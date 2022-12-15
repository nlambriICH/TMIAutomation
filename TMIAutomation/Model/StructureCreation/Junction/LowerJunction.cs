using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIAutomation.Async;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public class LowerJunction : IStructure
    {
        private readonly ILogger logger = Log.ForContext<LowerJunction>();
        private readonly EsapiWorker esapiWorker;
        private readonly string upperPlanId;
        private readonly string lowerPlanId;
        private readonly string lowerPTVId;
        private readonly string registrationId;

        public LowerJunction(EsapiWorker esapiWorker,
                            string upperPlanId,
                            string lowerPlanId,
                            string lowerPTVId,
                            string registrationId)
        {
            this.esapiWorker = esapiWorker;
            this.upperPlanId = upperPlanId;
            this.lowerPlanId = lowerPlanId;
            this.lowerPTVId = lowerPTVId;
            this.registrationId = registrationId;
        }

        public Task CreateAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("LowerJunction context: {@context}", new List<string> { this.upperPlanId, this.lowerPlanId, this.lowerPTVId, this.registrationId });

                Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                PlanSetup lowerPlan = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == this.lowerPlanId);
                StructureSet lowerSS = lowerPlan.StructureSet;
                Registration registration = scriptContext.Patient.Registrations.FirstOrDefault(reg => reg.Id == this.registrationId);

                message.Report("Generating isodose structures...");
                List<string> isoStructuresId = new List<string> { StructureHelper.DOSE_25, StructureHelper.DOSE_50, StructureHelper.DOSE_75, StructureHelper.DOSE_100 };
                if (!isoStructuresId.All(id => lowerSS.Structures.Select(s => s.Id).Contains(id)))
                {
                    CreateIsodoseStructures(targetCourse, registration, lowerPlan, progress, message);
                }

                message.Report("Generating junction structures...");
                progress.Report(0.3);
                CreateJunctionSubstructures(lowerSS);

                message.Report($"Generating {StructureHelper.REM} optimization structure and crop {StructureHelper.DOSE_100}...");
                progress.Report(0.3);
                CreateREMStructure(lowerSS);
                CropIsodose100(lowerSS);
            });
        }

        private void CreateIsodoseStructures(Course targetCourse,
                                             Registration registration,
                                             PlanSetup lowerPlan,
                                             IProgress<double> progress,
                                             IProgress<string> message)
        {
            /*
			* Isodose levels upper-body CT
			*/
            PlanSetup upperPlan = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == this.upperPlanId);
            List<double> doseValues = null;
            DoseValue.DoseUnit doseUnit = DoseValue.DoseUnit.Unknown;

            if (upperPlan.DoseValuePresentation == DoseValuePresentation.Absolute)
            {
                double totalDose = upperPlan.TotalDose.Dose;
                doseValues = new List<double> { 0.25, 0.50, 0.75, 1.0 };
                doseValues.ForEach(d => Math.Round(d * totalDose, 2, MidpointRounding.AwayFromZero));
                doseUnit = DoseValue.DoseUnit.Gy;
            }
            else
            {
                doseValues = new List<double> { 25.0, 50.0, 75.0, 100.0 };
                doseUnit = DoseValue.DoseUnit.Percent;
            }

            StructureSet upperSS = upperPlan.StructureSet;
            List<Structure> upperIsodoseStructures = new List<Structure>
            {
                upperSS.TryAddStructure("CONTROL", StructureHelper.DOSE_25, logger),
                upperSS.TryAddStructure("CONTROL", StructureHelper.DOSE_50, logger),
                upperSS.TryAddStructure("CONTROL", StructureHelper.DOSE_75, logger),
                upperSS.TryAddStructure("CONTROL", StructureHelper.DOSE_100, logger)
            };

            for (int i = 0; i < upperIsodoseStructures.Count; ++i)
            {
                message.Report($"Generating {upperIsodoseStructures[i].Id} structure...");
                progress.Report(0.05);
                upperIsodoseStructures[i].ConvertDoseLevelToStructure(upperPlan.Dose, new DoseValue(doseValues[i], doseUnit));
            }

            logger.Information("Structures created: {@bodyIsodoseStructures}", upperIsodoseStructures.Select(s => s.Id));

            /*
             * Copy structures to lower-extremities RTSTRUCT
             */
            StructureSet lowerSS = lowerPlan.StructureSet;
            upperIsodoseStructures.ForEach(upperIsoStructure =>
            {
                Structure lowerIsoStructure = lowerSS.TryAddStructure(upperIsoStructure.DicomType, upperIsoStructure.Id, logger);

                // do not modify already existing structure (not empty)
                if (!lowerIsoStructure.IsEmpty)
                {
                    logger.Information("Isodose structure {isodoseId} is not empty. Skip copying contours", upperIsoStructure.Id);
                    return;
                }

                progress.Report(0.05);
                logger.Information("Transform contours of {isodoseId}", upperIsoStructure.Id);

                bool stopCopyContour = false;
                List<int> upperIsoSlices = upperSS.GetStructureSlices(upperIsoStructure).ToList();
                int maxSliceNum = 0;
                foreach (int slice in upperIsoSlices)
                {
                    VVector[][] contours = upperIsoStructure.GetContoursOnImagePlane(slice);
                    logger.Verbose("Found {numContours} contours on upper-body slice {slice}", contours.Length, slice);

                    foreach (VVector[] contour in contours)
                    {
                        IEnumerable<VVector> transformedContour = contour.Select(vv => upperSS.Image.FOR == registration.SourceFOR ? registration.TransformPoint(vv) : registration.InverseTransformPoint(vv));

                        double dicomZ = transformedContour.FirstOrDefault().z;
                        int sliceZ = lowerSS.GetSlice(dicomZ);

                        if (slice == upperIsoSlices.FirstOrDefault())
                        {
                            maxSliceNum = lowerSS.Image.ZSize - sliceZ + 1;
                        }

                        if (sliceZ > lowerSS.Image.ZSize)
                        {
                            logger.Information("Slice {slice} exceeding lower-extremities CT ZSize ({zSize}). Stop copying contours", sliceZ, lowerSS.Image.ZSize);
                            stopCopyContour = true;
                            break;
                        }

                        message.Report($"Propagating contours of {upperIsoStructure.Id}. Slice {upperIsoSlices.IndexOf(slice) + 1}/{maxSliceNum}");

                        VVector vvUser = lowerSS.Image.DicomToUser(transformedContour.FirstOrDefault(), lowerPlan);
                        logger.Verbose("Transformed contour at DICOM={dicomZ}, User={userZ} mm, slice={slice}", Math.Round(dicomZ, 2), Math.Round(vvUser.z, 2), sliceZ);

                        lowerIsoStructure.AddContourOnImagePlane(transformedContour.ToArray(), sliceZ);
                    }

                    if (stopCopyContour) break;
                }

                /*
                 * in some patients, possibly due to the registration, contours were missing in one or more isolated slices
                 * applying an asymmetric margin to cover those slices (this also avoids issues when upper-body CT and lower-extremities CT have different ZRes)
                */
                lowerIsoStructure.SegmentVolume = lowerIsoStructure.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 0, 0, 0, 0, 0, lowerSS.Image.ZRes));
            });

            logger.Information("Isodose structures copied to lower-extremities StructureSet {ss} using registration: {registration}", lowerSS.Id, registration.Id);
        }

        private void CropIsodose100(StructureSet lowerSS)
        {
            Structure ptvTotal = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_TOTAL);
            Structure isodose100 = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_100);

            int bottomSlicePTVtotal = lowerSS.GetStructureSlices(ptvTotal).LastOrDefault();
            int bottomSliceIsodose100 = lowerSS.GetStructureSlices(isodose100).LastOrDefault();
            int cropOffset = 7;

            foreach (int slice in Enumerable.Range(bottomSlicePTVtotal + cropOffset, bottomSliceIsodose100 - bottomSlicePTVtotal - cropOffset + 1))
            {
                isodose100.ClearAllContoursOnImagePlane(slice);
            }

            logger.Information("Cropped structure: {isodose100}", isodose100.Id);

        }

        private void CreateREMStructure(StructureSet lowerSS)
        {
            /*
			 * Create "REM" structure
			 */
            Structure junction25 = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_JUNCTION25);

            int topSliceJunction25 = lowerSS.GetStructureSlices(junction25).FirstOrDefault();

            Structure ptvTotal = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_TOTAL);
            int bottomSlicePTVtotal = lowerSS.GetStructureSlices(ptvTotal).LastOrDefault();

            Structure rem = lowerSS.TryAddStructure("AVOIDANCE", StructureHelper.REM, logger);
            Structure isodose25 = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_25);

            foreach (int slice in Enumerable.Range(topSliceJunction25, bottomSlicePTVtotal - topSliceJunction25 + 3))
            {
                foreach (VVector[] contour in isodose25.GetContoursOnImagePlane(slice))
                {
                    rem.AddContourOnImagePlane(contour, slice);
                }
            }

            Structure lowerJunction = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.LOWER_PTV_JUNCTION);
            rem.SegmentVolume = rem.Sub(lowerJunction.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 10, 10, 0, 10, 10, 0)));

            logger.Information("Structure created: {rem}", rem.Id);
        }

        private void CreateJunctionSubstructures(StructureSet lowerSS)
        {
            /*
			* Create junction substructures for lower-extremities starting from isodoses
			*/
            Structure lowerPTVStart = lowerSS.Structures.FirstOrDefault(s => s.Id == this.lowerPTVId);
            Structure lowerPTV = lowerSS.TryAddStructure(lowerPTVStart.DicomType, StructureHelper.PTV_TOTAL, logger);
            lowerPTV.SegmentVolume = lowerPTVStart.SegmentVolume;
            IEnumerable<int> juncSlices = lowerSS.GetStructureSlices(lowerPTVStart);

            Structure isodose100 = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_100);
            IEnumerable<int> isoSlices = lowerSS.GetStructureSlices(isodose100);

            // clear contours of ptv on slices where isodose100% is present
            foreach (int slice in juncSlices.Intersect(isoSlices))
            {
                lowerPTV.ClearAllContoursOnImagePlane(slice);
            }

            Structure isodose75 = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_75);
            Structure junction25 = lowerSS.CreateStructureFromIsodose(StructureHelper.PTV_JUNCTION25, lowerPTV, isodose75, logger);
            logger.Information("Structure created: {junction25}", junction25.Id);

            Structure isodose50 = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_50);
            Structure junction50 = lowerSS.CreateStructureFromIsodose(StructureHelper.PTV_JUNCTION50, lowerPTV, isodose50, logger);
            logger.Information("Structure created: {junction50}", junction50.Id);

            Structure isodose25 = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_25);
            Structure junction75 = lowerSS.CreateStructureFromIsodose(StructureHelper.PTV_JUNCTION75, lowerPTV, isodose25, logger);
            logger.Information("Structure created: {junction75}", junction75.Id);

            junction75.SegmentVolume = junction75.Sub(junction50.SegmentVolume);
            junction50.SegmentVolume = junction50.Sub(junction25.SegmentVolume);

            Structure junction100 = lowerSS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION100, logger);
            // Z-axis points towards the gantry: the first slice is the uppermost when patient is in FFS
            int topSliceJunction75 = lowerSS.GetStructureSlices(junction75).FirstOrDefault();
            for (int i = 1; i <= 4; ++i) // generate PTV_Junction100% with 4 slices
            {
                foreach (VVector[] contour in lowerPTV.GetContoursOnImagePlane(topSliceJunction75 - i))
                {
                    junction100.AddContourOnImagePlane(contour, topSliceJunction75 - i);
                }
            }
            logger.Information("Structure created: {junction100}", junction100.Id);

            Structure lowerJunction = lowerSS.TryAddStructure("PTV", StructureHelper.LOWER_PTV_JUNCTION, logger);
            lowerJunction.SegmentVolume = junction25.Or(junction50).Or(junction75).Or(junction100);
            logger.Information("Structure created: {lowerJunction}", lowerJunction.Id);

            Structure lowerPTVNoJ = lowerSS.TryAddStructure("PTV", StructureHelper.LOWER_PTV_NO_JUNCTION, logger);
            lowerPTVNoJ.SegmentVolume = lowerPTV.Sub(lowerJunction);
            logger.Information("Structure created: {lowerPTVNoJ}", lowerPTVNoJ.Id);
        }
    }
}
