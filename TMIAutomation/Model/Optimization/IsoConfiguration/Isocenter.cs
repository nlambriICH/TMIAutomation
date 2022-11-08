using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class Isocenter
    {
        private static readonly ILogger logger = Log.ForContext(typeof(Isocenter));

        public static void SetIsocenters(this ExternalPlanSetup targetPlan, string machineName)
        {
            foreach (Beam beam in targetPlan.Beams)
            {
                logger.Information("Removing existing beam: {beamID}", beam.Id);
                targetPlan.RemoveBeam(beam);
            }

            StructureSet lowerSS = targetPlan.StructureSet;
            Structure lowerPTVTotal = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_TOTAL);

            // The Rect3D X, Y, Z are placed near the feet
            Rect3D rect = lowerPTVTotal.MeshGeometry.Bounds;
            double isoStep = rect.SizeZ / 4;

            logger.Information("Using isocenter step [mm]: {isoStep}", Math.Round(isoStep, MidpointRounding.AwayFromZero));

            // Isocenter positions in CC direction
            List<VVector> isoPositions = new List<VVector>
            {
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y, rect.Z + rect.SizeZ - (0.75 * isoStep)),
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y, rect.Z + rect.SizeZ - (2 * isoStep)),
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y - 30, rect.Z + rect.SizeZ - (3.35 * isoStep)) // shift y for the feet
            };

            double fieldY = (rect.SizeX / 2) + 15; // Y1 and Y2 jaw aperture
            double feetFieldY = (rect.SizeX / 2) + 30; // Y1 and Y2 jaw aperture

            if (feetFieldY > 200) feetFieldY = 200; // maximum field aperture

            double fieldXIso12 = (1.25 * isoStep / 2) + 20; // half z-distance iso + 20 mm to obtain approx 40 mm of overlap
            double fieldXIso23 = (1.35 * isoStep / 2) + 20; // half z-distance iso + 20 mm to obtain approx 40 mm of overlap

            // Jaw positions for the isocenters. X1 towards the head
            List<Tuple<VRect<double>, VRect<double>>> jawPositions = new List<Tuple<VRect<double>, VRect<double>>>
            {
                Tuple.Create(
                    new VRect<double>(-isoStep, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -fieldY, fieldXIso12, fieldY)
                    ),
                Tuple.Create(
                    new VRect<double>(-fieldXIso12, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -fieldY, fieldXIso23, fieldY)
                    ),
                Tuple.Create(
                    new VRect<double>(-fieldXIso23, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -feetFieldY, isoStep, feetFieldY)
                    )
            };

            logger.Information("Overlap between fields same isocenter [mm]: {overlapSameIso}", 20);
            logger.Information("Overlap between fields adjacent isocenters [mm]: {overlapAdjIso}", Math.Round(isoStep * .2, MidpointRounding.AwayFromZero));

            for (int i = 0; i < isoPositions.Count(); ++i)
            {
                logger.Information("Adding fields at isocenter {num}. Isocenter coordinates [mm]: {@isocenter}", i + 1, isoPositions[i]);

                logger.Information("Field {first}. Jaw position [mm]: {@jawPosition}", (2 * i) + 1, jawPositions[i].Item1);

                targetPlan.AddArcBeam(
                    new ExternalBeamMachineParameters(machineName, "6X", 600, "ARC", ""),
                    jawPositions[i].Item1,
                    90,
                    180.1,
                    179.9,
                    GantryDirection.Clockwise,
                    0,
                    isoPositions[i]
                );

                logger.Information("Field {second}. Jaw position [mm]: {@jawPosition}", (2 * i) + 2, jawPositions[i].Item2);

                targetPlan.AddArcBeam(
                    new ExternalBeamMachineParameters(machineName, "6X", 600, "ARC", ""),
                    jawPositions[i].Item2,
                    90,
                    179.9,
                    180.1,
                    GantryDirection.CounterClockwise,
                    0,
                    isoPositions[i]
                );
            }
        }

        public static void CopyCaudalIsocenter(this ExternalPlanSetup targetPlan,
                                               ExternalPlanSetup sourcePlan,
                                               Registration registration,
                                               string machineName)
        {
            IEnumerable<Beam> beamsCaudalIso = sourcePlan.Beams.Skip(Math.Max(0, sourcePlan.Beams.Count() - 2));
            foreach (Beam beam in beamsCaudalIso)
            {
                VVector beamIso = beam.IsocenterPosition;
                VVector transformedIso = sourcePlan.StructureSet.Image.FOR == registration.SourceFOR
                    ? registration.TransformPoint(beamIso)
                    : registration.InverseTransformPoint(beamIso);

                BeamParameters beamParams = beam.GetEditableParameters();
                List<ControlPointParameters> cpParams = beamParams.ControlPoints.ToList();

                double collAngle = cpParams.First().CollimatorAngle;
                double transformedCollAngle =  collAngle > 180 ? collAngle - 180 : 180 + collAngle;
                double transformedGantryAngle = cpParams.Last().GantryAngle;
                double transformedGantryStop = cpParams.First().GantryAngle;

                Beam newBeam = targetPlan.AddVMATBeam(
                    new ExternalBeamMachineParameters(machineName, "6X", 600, "ARC", ""),
                    cpParams.Select(cp => cp.MetersetWeight),
                    transformedCollAngle,
                    transformedGantryAngle,
                    transformedGantryStop,
                    beamParams.GantryDirection == GantryDirection.Clockwise ? GantryDirection.CounterClockwise : GantryDirection.Clockwise,
                    0,
                    transformedIso
                );

                newBeam.Id = beam.Id;

                BeamParameters newBeamParams = newBeam.GetEditableParameters();
                /* ISSUE: CalculateDoseWithPresetValues does not work for VMAT
                 * set expected MUs with weight factor
                 */
                newBeamParams.WeightFactor = beam.WeightFactor;
                VRect<double> jawPos = cpParams.First().JawPositions; // assuming no jaw tracking
                newBeamParams.SetJawPositions(jawPos);
                List<ControlPointParameters> newBeamCPParams = newBeamParams.ControlPoints.ToList();
                foreach (ControlPointParameters cpUpperBeam in cpParams)
                {
                    newBeamCPParams.FirstOrDefault(cpNewBeam => cpNewBeam.Index == cpUpperBeam.Index).LeafPositions = cpUpperBeam.LeafPositions;
                }
                newBeam.ApplyParameters(newBeamParams);
                logger.Information("Caudal field {beam} copied to lower dose-base plan", newBeam.Id);
            }
        }
    }
}
