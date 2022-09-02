﻿using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIJunction.Async;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIJunction
{
    public class LegsJunction : IStructure
    {
        private readonly PlanSetup upperPlan;
        private readonly PlanSetup legsPlan;
        private readonly string legsPTVId;
        private readonly string imageRegId;
        private readonly ILogger logger = Log.ForContext<LegsJunction>();
        private readonly EsapiWorker esapiWorker;

        public LegsJunction(EsapiWorker esapiWorker)
        {
            this.esapiWorker = esapiWorker;
        }

        public Task<List<string>> GetLowerPlans()
        {
            return esapiWorker.RunAsync(scriptContext =>
            {
                Course latestCourse = scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                return latestCourse.PlanSetups.Where(p => p.Id.Contains("down"))
                                  .OrderByDescending(p => p.CreationDateTime)
                                  .Select(p => p.Id)
                                  .ToList();
            });
        }

        public Task<List<string>> GetUpperPlans()
        {
            return esapiWorker.RunAsync(scriptContext =>
            {
                Course latestCourse = scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                return latestCourse.PlanSetups.Where(p => p.Id.Contains("up"))
                                  .OrderByDescending(p => p.CreationDateTime)
                                  .Select(p => p.Id)
                                  .ToList();
            });
        }

        public Task CreateAsync(IProgress<double> progress, IProgress<string> message)
        {
            return esapiWorker.RunAsync(ScriptContext =>
            {
                logger.Information("LegsJunction context: {@context}", new List<string> { upperPlan.Id, legsPlan.Id, legsPTVId, imageRegId });

                StructureSet upperSS = upperPlan.StructureSet;
                StructureSet legsSS = legsPlan.StructureSet;

                List<string> isoStructuresId = new List<string> { StructureHelper.DOSE_25, StructureHelper.DOSE_50, StructureHelper.DOSE_75, StructureHelper.DOSE_100 };

                if (!isoStructuresId.All(id => legsSS.Structures.Select(s => s.Id).Contains(id)))
                {
                    /*
					 * Isodose levels upper CT
					 */
                    List<double> doseValues = null;
                    DoseValue.DoseUnit doseUnit = DoseValue.DoseUnit.Unknown;

                    if (upperPlan.DoseValuePresentation == DoseValuePresentation.Absolute)
                    {
                        double dosePerFraction = upperPlan.DosePerFraction.Dose;
                        doseValues = new List<double> { 0.25, 0.50, 0.75, 1.0 };
                        doseValues.ForEach(d => Math.Round(d * dosePerFraction, 2, MidpointRounding.AwayFromZero));
                        doseUnit = DoseValue.DoseUnit.Gy;
                    }
                    else
                    {
                        doseValues = new List<double> { 25.0, 50.0, 75.0, 100.0 };
                        doseUnit = DoseValue.DoseUnit.Percent;
                    }

                    List<Structure> upperIsodoseStructures = new List<Structure>
                    {
                        upperSS.TryAddStructure("CONTROL", StructureHelper.DOSE_25, logger),
                        upperSS.TryAddStructure("CONTROL", StructureHelper.DOSE_50, logger),
                        upperSS.TryAddStructure("CONTROL", StructureHelper.DOSE_75, logger),
                        upperSS.TryAddStructure("CONTROL", StructureHelper.DOSE_100, logger)
                    };

                    for (int i = 0; i < upperIsodoseStructures.Count; ++i)
                    {
                        upperIsodoseStructures[i].ConvertDoseLevelToStructure(upperPlan.Dose, new DoseValue(doseValues[i], doseUnit));
                    }

                    logger.Information("Structures created: {@bodyIsodoseStructures}", upperIsodoseStructures.Select(s => s.Id));

                    /*
					 * Copy structures to legs RTSTRUCT
					 */
                    Registration registration = ScriptContext.Patient.Registrations.FirstOrDefault(reg => reg.Id == imageRegId);

                    upperIsodoseStructures.ForEach(isoStructure =>
                    {
                        Structure legIsoDose = legsSS.TryAddStructure(isoStructure.DicomType, isoStructure.Id, logger);

                        // do not modify already existing structure (not empty)
                        if (!legIsoDose.IsEmpty)
                        {
                            logger.Information("Isodose structure {isodoseId} is not empty. Skip copying contours", isoStructure.Id);
                            return;
                        }

                        WindowHelper.ShowAutoClosingMessageBox($"Propagating contours of {isoStructure.Id}...", "Isodose Propagation");

                        logger.Information("Transform contours of {isodoseId}", isoStructure.Id);

                        bool stopCopyContour = false;
                        foreach (int slice in upperSS.GetStructureSlices(isoStructure))
                        {
                            VVector[][] contours = isoStructure.GetContoursOnImagePlane(slice);
                            logger.Verbose("Found {numContours} contours on upper-body slice {slice}", contours.Length, slice);

                            foreach (VVector[] contour in contours)
                            {
                                IEnumerable<VVector> transformedContour = contour.Select(vv => upperSS.Image.FOR == registration.SourceFOR ? registration.TransformPoint(vv) : registration.InverseTransformPoint(vv));

                                double dicomZ = transformedContour.FirstOrDefault().z;
                                var sliceZ = legsSS.GetSlice(dicomZ);

                                if (sliceZ > legsSS.Image.ZSize)
                                {
                                    logger.Information("Slice {slice} exceeding Legs CT ZSize ({zSize}). Stop copying contours", sliceZ, legsSS.Image.ZSize);
                                    stopCopyContour = true;
                                    break;
                                }

                                VVector vvUser = legsSS.Image.DicomToUser(transformedContour.FirstOrDefault(), legsPlan);

                                logger.Verbose("Transformed contour at DICOM={dicomZ}, User={userZ}mm, slice={slice}", Math.Round(dicomZ, 2), Math.Round(vvUser.z, 2), sliceZ);

                                legIsoDose.AddContourOnImagePlane(transformedContour.ToArray(), sliceZ);
                            }

                            if (stopCopyContour) break;
                        }

                        /*
						 * in some patients, possibly due to the registration, contours were missing in one or more isolated slices
						 * applying an asymmetric margin to cover those slices (this also avoids issues when upper-body CT and lower-extremities CT have different ZRes)
						*/
                        legIsoDose.SegmentVolume = legIsoDose.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 0, 0, 0, 0, 0, legsSS.Image.ZRes));
                    });

                    logger.Information("Isodose structures copied to legs StructureSet {ss} using registration: {registration}", legsSS.Id, registration.Id);

                }

                WindowHelper.ShowAutoClosingMessageBox($"Generating junction structures...", "TMIJunction");
                CreateJunctionSubstructures(legsSS);
                CreateREMStructure(legsSS);
                CropIsodose100(legsSS);
            });
        }

        private void CropIsodose100(StructureSet legsSS)
        {
            Structure ptvTotal = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_TOTAL);
            Structure isodose100 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_100);

            int bottomSlicePTVtotal = legsSS.GetStructureSlices(ptvTotal).LastOrDefault();
            int bottomSliceIsodose100 = legsSS.GetStructureSlices(isodose100).LastOrDefault();
            int cropOffset = 7;

            foreach (int slice in Enumerable.Range(bottomSlicePTVtotal + cropOffset, bottomSliceIsodose100 - bottomSlicePTVtotal - cropOffset + 1))
            {
                isodose100.ClearAllContoursOnImagePlane(slice);
            }

            logger.Information("Cropped structure: {isodose100}", isodose100.Id);

        }

        private void CreateREMStructure(StructureSet legsSS)
        {
            /*
			 * Create "REM" structure
			 */
            Structure junction25 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_JUNCTION25);

            int topSliceJunction25 = legsSS.GetStructureSlices(junction25).FirstOrDefault();

            Structure ptvTotal = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_TOTAL);
            int bottomSlicePTVtotal = legsSS.GetStructureSlices(ptvTotal).LastOrDefault();

            Structure rem = legsSS.TryAddStructure("AVOIDANCE", StructureHelper.REM, logger);
            Structure isodose25 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_25);

            foreach (int slice in Enumerable.Range(topSliceJunction25, bottomSlicePTVtotal - topSliceJunction25 + 3))
            {
                foreach (VVector[] contour in isodose25.GetContoursOnImagePlane(slice))
                {
                    rem.AddContourOnImagePlane(contour, slice);
                }
            }

            Structure legsJunction = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.LOWER_PTV_JUNCTION);
            rem.SegmentVolume = rem.Sub(legsJunction.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 10, 10, 0, 10, 10, 0)));

            logger.Information("Structure created: {rem}", rem.Id);
        }

        private void CreateJunctionSubstructures(StructureSet legsSS)
        {
            /*
			* Create junction substructures for legs starting from isodoses
			*/
            Structure ptv = legsSS.Structures.FirstOrDefault(s => s.Id == legsPTVId);
            Structure ptvLegsWithJunction = legsSS.TryAddStructure(ptv.DicomType, StructureHelper.PTV_TOTAL, logger);
            ptvLegsWithJunction.SegmentVolume = ptv.SegmentVolume;
            IEnumerable<int> juncSlices = legsSS.GetStructureSlices(ptv);

            Structure isodose100 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_100);
            IEnumerable<int> isoSlices = legsSS.GetStructureSlices(isodose100);

            // clear contours of ptv on slices where isodose100% is present
            foreach (int slice in juncSlices.Intersect(isoSlices))
            {
                ptvLegsWithJunction.ClearAllContoursOnImagePlane(slice);
            }

            Structure isodose75 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_75);
            Structure junction25 = legsSS.CreateStructureFromIsodose(StructureHelper.PTV_JUNCTION25, ptvLegsWithJunction, isodose75, logger);
            logger.Information("Structure created: {junction25}", junction25.Id);

            Structure isodose50 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_50);
            Structure junction50 = legsSS.CreateStructureFromIsodose(StructureHelper.PTV_JUNCTION50, ptvLegsWithJunction, isodose50, logger);
            logger.Information("Structure created: {junction50}", junction50.Id);

            Structure isodose25 = legsSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_25);
            Structure junction75 = legsSS.CreateStructureFromIsodose(StructureHelper.PTV_JUNCTION75, ptvLegsWithJunction, isodose25, logger);
            logger.Information("Structure created: {junction75}", junction75.Id);

            junction75.SegmentVolume = junction75.Sub(junction50.SegmentVolume);
            junction50.SegmentVolume = junction50.Sub(junction25.SegmentVolume);

            Structure junction100 = legsSS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION100, logger);
            // Z-axis points towards the gantry: the first slice is the uppermost when patient is in FFS
            int topSliceJunction75 = legsSS.GetStructureSlices(junction75).FirstOrDefault();
            for (int i = 1; i <= 4; ++i) // generate PTV_Junction100% with 4 slices
            {
                foreach (VVector[] contour in ptvLegsWithJunction.GetContoursOnImagePlane(topSliceJunction75 - i))
                {
                    junction100.AddContourOnImagePlane(contour, topSliceJunction75 - i);
                }
            }
            logger.Information("Structure created: {junction100}", junction100.Id);

            Structure legsJunction = legsSS.TryAddStructure("PTV", StructureHelper.LOWER_PTV_JUNCTION, logger);
            legsJunction.SegmentVolume = junction25.Or(junction50).Or(junction75).Or(junction100);
            logger.Information("Structure created: {legsJunction}", legsJunction.Id);

            Structure ptvTotNoJunctionLegs = legsSS.TryAddStructure("PTV", StructureHelper.LOWER_PTV_NO_JUNCTION, logger);
            ptvTotNoJunctionLegs.SegmentVolume = ptvLegsWithJunction.Sub(legsJunction);
            logger.Information("Structure created: {ptvTotNoJunctionLegs}", ptvTotNoJunctionLegs.Id);
        }
    }
}
