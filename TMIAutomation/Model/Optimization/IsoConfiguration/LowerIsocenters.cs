using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using Serilog;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class LowerIsocenters
    {
        private static readonly ILogger logger = Log.ForContext(typeof(LowerIsocenters));

        public static void SetIsocentersLower(this ExternalPlanSetup targetPlan, ExternalPlanSetup sourcePlan)
        {
            targetPlan.ClearBeams();

            StructureSet lowerSS = targetPlan.StructureSet;
            Structure lowerPTVTotal = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_TOTAL)
                ?? throw new ApplicationException("Create the structure 'PTV_Total = LowerPTVNoJ + LowerPTV_J' to setup the isocenters for the lower extremities.");

            Beam beamSourcePlan = sourcePlan.Beams.FirstOrDefault(b => !b.IsSetupField);
            ExternalBeamMachineParameters sourcePlanBeamParams = new ExternalBeamMachineParameters(beamSourcePlan.TreatmentUnit.Id,
                                                                                                   beamSourcePlan.EnergyModeDisplayName,
                                                                                                   beamSourcePlan.DoseRate,
                                                                                                   beamSourcePlan.Technique.Id,
                                                                                                   "");
            logger.Information("Using beam machine parameters: {@sourcePlanBeamParams} retrieved from plan {sourcePlan}", sourcePlanBeamParams, sourcePlan.Id);

            if (ConfigOptOptions.LowerExtremitiesCollimator == "90")
            {
                DefineIsocentersAndJaws90(lowerPTVTotal,
                                        out List<VVector> isoPositions,
                                        out List<Tuple<VRect<double>, VRect<double>>> jawPositions);

                for (int i = 0; i < isoPositions.Count(); ++i)
                {
                    targetPlan.LogNewBeamInfo(isoPositions[i], jawPositions[i].Item1);
                    targetPlan.AddArcBeam(
                        sourcePlanBeamParams,
                        jawPositions[i].Item1,
                        5,
                        179.9,
                        180.1,
                        GantryDirection.CounterClockwise,
                        0,
                        isoPositions[i]
                    );

                    targetPlan.LogNewBeamInfo(isoPositions[i], jawPositions[i].Item2);
                    targetPlan.AddArcBeam(
                        sourcePlanBeamParams,
                        jawPositions[i].Item2,
                        355,
                        180.1,
                        179.9,
                        GantryDirection.Clockwise,
                        0,
                        isoPositions[i]
                    );
                }
            }
            else
            {
                DefineIsocentersAndJaws5355(lowerPTVTotal,
                                            out List<VVector> isoPositions,
                                            out List<Tuple<VRect<double>, VRect<double>>> jawPositions);

                for (int i = 0; i < isoPositions.Take(2).Count(); ++i)
                {
                    targetPlan.LogNewBeamInfo(isoPositions[i], jawPositions[i].Item1);
                    targetPlan.AddArcBeam(
                        sourcePlanBeamParams,
                        jawPositions[i].Item1,
                        5,
                        179.9,
                        180.1,
                        GantryDirection.CounterClockwise,
                        0,
                        isoPositions[i]
                    );

                    targetPlan.LogNewBeamInfo(isoPositions[i], jawPositions[i].Item2);
                    targetPlan.AddArcBeam(
                        sourcePlanBeamParams,
                        jawPositions[i].Item2,
                        355,
                        180.1,
                        179.9,
                        GantryDirection.Clockwise,
                        0,
                        isoPositions[i]
                    );
                }

                if (isoPositions.Count() == 3)
                {
                    targetPlan.LogNewBeamInfo(isoPositions[2], jawPositions[2].Item1);
                    targetPlan.AddArcBeam(
                        sourcePlanBeamParams,
                        jawPositions[2].Item1,
                        90,
                        179.9,
                        180.1,
                        GantryDirection.CounterClockwise,
                        0,
                        isoPositions[2]
                    );
                }
            }
        }

        private static void DefineIsocentersAndJaws90(Structure lowerPTVTotal,
                                                    out List<VVector> isoPositions,
                                                    out List<Tuple<VRect<double>, VRect<double>>> jawPositions)
        {
            // The Rect3D X, Y, Z are placed near the feet
            Rect3D rect = lowerPTVTotal.MeshGeometry.Bounds;
            double isoStep = rect.SizeZ / 4;
            logger.Information("Using isocenter step [mm]: {isoStep}", Math.Round(isoStep, MidpointRounding.AwayFromZero));

            // Isocenter positions in CC direction
            isoPositions = new List<VVector>
            {
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y, rect.Z + rect.SizeZ - (0.75 * isoStep)),
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y, rect.Z + rect.SizeZ - (2 * isoStep)),
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y - 30, rect.Z + rect.SizeZ - (3.35 * isoStep)) // shift y for the feet
            };
            double fieldY = (rect.SizeX / 2) + 15; // Y1 and Y2 jaw aperture
            double feetFieldY = (rect.SizeX / 2) + 30; // Y1 and Y2 jaw aperture

            if (feetFieldY > 200)
            {
                logger.Information("Reducing feet Y-field aperture [mm] from {feetFieldY} to 200", Math.Round(feetFieldY, MidpointRounding.AwayFromZero));
                feetFieldY = 200; // maximum field aperture
            }

            double fieldXIso12 = (1.25 * isoStep / 2) + 20; // half z-distance iso + 20 mm to obtain approx 40 mm of overlap
            double fieldXIso23 = (1.35 * isoStep / 2) + 20; // half z-distance iso + 20 mm to obtain approx 40 mm of overlap

            // Ensure most cranial and caudal fields have X<=200
            double field1X1 = isoStep > 200 ? 200 : isoStep;
            double field3X2 = isoStep > 200 ? 200 : isoStep;

            // Jaw positions for the isocenters. X1 towards the head
            jawPositions = new List<Tuple<VRect<double>, VRect<double>>>
            {
                Tuple.Create(
                    new VRect<double>(-field1X1, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -fieldY, fieldXIso12, fieldY)
                    ),
                Tuple.Create(
                    new VRect<double>(-fieldXIso12, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -fieldY, fieldXIso23, fieldY)
                    ),
                Tuple.Create(
                    new VRect<double>(-fieldXIso23, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -feetFieldY, field3X2, feetFieldY)
                    )
            };

            logger.Information("Overlap between fields same isocenter [mm]: {overlapSameIso}", 20);
            logger.Information("Overlap between fields adjacent isocenters [mm]: {overlapAdjIso}", Math.Round(isoStep * .2, MidpointRounding.AwayFromZero));
        }

        private static void DefineIsocentersAndJaws5355(Structure lowerPTVTotal,
                                                        out List<VVector> isoPositions,
                                                        out List<Tuple<VRect<double>, VRect<double>>> jawPositions)
        {
            // The Rect3D X, Y, Z are placed near the feet
            Rect3D rect = lowerPTVTotal.MeshGeometry.Bounds;
            double isoStep = 170;
            isoPositions = new List<VVector>
            {
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y, rect.Z + rect.SizeZ - (0.9 * isoStep)),
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y, rect.Z + rect.SizeZ - (2.7 * isoStep)),
            };
            jawPositions = new List<Tuple<VRect<double>, VRect<double>>>
            {
                Tuple.Create(new VRect<double>(-170, -200, 30, 200), new VRect<double>(-30, -200, 170, 200)),
                Tuple.Create(new VRect<double>(-170, -200, 30, 200), new VRect<double>(-30, -200, 170, 200)),
            };

            // Additional iso with one beam at 90 deg if long patient. X1 towards the head
            if (rect.SizeZ > 650)
            {
                isoPositions.Add(new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y, rect.Z + rect.SizeZ - (4.2 * isoStep)));
                jawPositions.Add(Tuple.Create(new VRect<double>(-120, -200, 120, 200), new VRect<double>()));
            }
        }
    }
}
