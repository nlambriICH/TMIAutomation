using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using Serilog;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class Isocenters
    {
        private static readonly ILogger logger = Log.ForContext(typeof(Isocenters));

        public static void SetIsocentersUpper(this ExternalPlanSetup targetPlan,
                                              string modelName,
                                              Structure upperPTV,
                                              Dictionary<string, List<List<double>>> fieldGeometry)
        {
            targetPlan.ClearBeams();
            List<List<double>> isocenters = fieldGeometry["Isocenters"];
            List<List<double>> jawX = fieldGeometry["Jaw_X"];
            List<List<double>> jawY = fieldGeometry["Jaw_Y"];

            // Round isocenter's z-coord with respect to UserOrigin and assign PTV center point for y-coord
            double isoCoordY = upperPTV.CenterPoint.y;
            List<VVector> isoRounded = new List<VVector>();
            foreach (List<double> iso in isocenters)
            {
                VVector isoUserCoord = targetPlan.StructureSet.Image.DicomToUser(new VVector(iso[0], isoCoordY, iso[2]), targetPlan);
                isoUserCoord.x = Math.Round(isoUserCoord.x / 10, 0) * 10;
                isoUserCoord.y = Math.Round(isoUserCoord.y / 10, 0) * 10;
                isoUserCoord.z = Math.Round(isoUserCoord.z / 10, 0) * 10;
                VVector isoDicom = targetPlan.StructureSet.Image.UserToDicom(isoUserCoord, targetPlan);
                isoRounded.Add(isoDicom);
            }

            ExternalBeamMachineParameters sourcePlanBeamParams = new ExternalBeamMachineParameters(ConfigOptOptions.TreatmentMachine,
                                                                                                   "6X",
                                                                                                   600,
                                                                                                   "ARC",
                                                                                                   "");
            double collimatorAngleFirstIso = 90;
            double collimatorAngleSecondIso = 90;
            if (modelName == Client.MODEL_NAME_BODY)
            {
                for (int i = isocenters.Count() - 3; i >= 0; i -= 2) // -3 because of body model (skip last 2 iso for arms)
                {
                    int firstIsoInGroup = i - 1;
                    VVector isocenter = isoRounded[firstIsoInGroup];
                    if (i == 9) isocenter.y -= 20; // shift y-coord toward the brain (anterior)
                    VRect<double> jawPositions = new VRect<double>(jawX[firstIsoInGroup][0], jawY[firstIsoInGroup][0], jawX[firstIsoInGroup][1], jawY[firstIsoInGroup][1]);
                    LogNewBeamInfo(targetPlan, isocenter, jawPositions);
                    
                    if (i == 1 && Client.collPelvis)
                    {
                        collimatorAngleFirstIso = 355;
                        collimatorAngleSecondIso = 5;
                    }

                    targetPlan.AddArcBeam(
                        sourcePlanBeamParams,
                        jawPositions,
                        collimatorAngleFirstIso,
                        179.9,
                        180.1,
                        GantryDirection.CounterClockwise,
                        0,
                        isocenter
                    );

                    int secondIsoInGroup = i;
                    isocenter = isoRounded[secondIsoInGroup];
                    if (i == 9) isocenter.y -= 20; // shift y-coord toward the brain (anterior)
                    jawPositions = new VRect<double>(jawX[secondIsoInGroup][0], jawY[secondIsoInGroup][0], jawX[secondIsoInGroup][1], jawY[secondIsoInGroup][1]);
                    LogNewBeamInfo(targetPlan, isocenter, jawPositions);
                    targetPlan.AddArcBeam(
                        sourcePlanBeamParams,
                        jawPositions,
                        collimatorAngleSecondIso,
                        180.1,
                        179.9,
                        GantryDirection.Clockwise,
                        0,
                        isocenter
                    );
                }
            }
            else if (modelName == Client.MODEL_NAME_ARMS)
            {
                for (int i = isocenters.Count() - 3; i >= 2; i -= 2) // -3 to skip last 2 iso for arms and >= 2 to skip pelvis iso
                {
                    if (i == 5) continue; // skip thorax isocenter not present with iso on arms
                    int firstIsoInGroup = i - 1;
                    VVector isocenter = isoRounded[firstIsoInGroup];
                    if (i == 9) isocenter.y -= 20; // shift y-coord toward the brain (anterior)
                    VRect<double> jawPositions = new VRect<double>(jawX[firstIsoInGroup][0], jawY[firstIsoInGroup][0], jawX[firstIsoInGroup][1], jawY[firstIsoInGroup][1]);
                    LogNewBeamInfo(targetPlan, isocenter, jawPositions);
                    targetPlan.AddArcBeam(
                        sourcePlanBeamParams,
                        jawPositions,
                        collimatorAngleFirstIso,
                        179.9,
                        180.1,
                        GantryDirection.CounterClockwise,
                        0,
                        isocenter
                    );

                    int secondIsoInGroup = i;
                    isocenter = isoRounded[secondIsoInGroup];
                    if (i == 9) isocenter.y -= 20; // shift y-coord toward the brain (anterior)
                    jawPositions = new VRect<double>(jawX[secondIsoInGroup][0], jawY[secondIsoInGroup][0], jawX[secondIsoInGroup][1], jawY[secondIsoInGroup][1]);
                    LogNewBeamInfo(targetPlan, isocenter, jawPositions);
                    targetPlan.AddArcBeam(
                        sourcePlanBeamParams,
                        jawPositions,
                        collimatorAngleSecondIso,
                        180.1,
                        179.9,
                        GantryDirection.Clockwise,
                        0,
                        isocenter
                    );
                }

                // Isocenter right arm
                int indexIsoRightArm = isocenters.Count - 1;
                VVector isocenterRightArm = isoRounded[indexIsoRightArm];
                isocenterRightArm.y += 40; // shift y-coord toward the arm (posterior)
                VRect<double> jawPositionsRightArm = new VRect<double>(jawX[indexIsoRightArm][0], jawY[indexIsoRightArm][0], jawX[indexIsoRightArm][1], jawY[indexIsoRightArm][1]);
                LogNewBeamInfo(targetPlan, isocenterRightArm, jawPositionsRightArm);
                targetPlan.AddArcBeam(
                    sourcePlanBeamParams,
                    jawPositionsRightArm,
                    355,
                    179.9,
                    355.0,
                    GantryDirection.CounterClockwise,
                    0,
                    isocenterRightArm
                );

                // Isocenter left arm
                int indexIsoLeftArm = isocenters.Count - 2;
                VVector isocenterLeftArm = isoRounded[indexIsoLeftArm];
                isocenterLeftArm.y += 40; // shift y-coord toward the arm (posterior)
                VRect<double> jawPositionsLeftArm = new VRect<double>(jawX[indexIsoLeftArm][0], jawY[indexIsoLeftArm][0], jawX[indexIsoLeftArm][1], jawY[indexIsoLeftArm][1]);
                LogNewBeamInfo(targetPlan, isocenterLeftArm, jawPositionsLeftArm);
                targetPlan.AddArcBeam(
                    sourcePlanBeamParams,
                    jawPositionsLeftArm,
                    5,
                    180.1,
                    5.0,
                    GantryDirection.Clockwise,
                    0,
                    isocenterLeftArm
                );

                // Isocenters pelvis
                if (Client.collPelvis)
                {
                    collimatorAngleFirstIso = 355;
                    collimatorAngleSecondIso = 5;
                }

                VVector isocenterPelivs = isoRounded[0];
                VRect<double> jawPositionsPelvis = new VRect<double>(jawX[0][0], jawY[0][0], jawX[0][1], jawY[0][1]);
                LogNewBeamInfo(targetPlan, isocenterPelivs, jawPositionsPelvis);
                targetPlan.AddArcBeam(
                    sourcePlanBeamParams,
                    jawPositionsPelvis,
                    collimatorAngleFirstIso,
                    179.9,
                    180.1,
                    GantryDirection.CounterClockwise,
                    0,
                    isocenterPelivs
                );

                isocenterPelivs = isoRounded[1];
                jawPositionsPelvis = new VRect<double>(jawX[1][0], jawY[1][0], jawX[1][1], jawY[1][1]);
                LogNewBeamInfo(targetPlan, isocenterPelivs, jawPositionsPelvis);
                targetPlan.AddArcBeam(
                    sourcePlanBeamParams,
                    jawPositionsPelvis,
                    collimatorAngleSecondIso,
                    180.1,
                    179.9,
                    GantryDirection.Clockwise,
                    0,
                    isocenterPelivs
                );
            }
        }

        private static void LogNewBeamInfo(ExternalPlanSetup targetPlan, VVector isocenter, VRect<double> jawPositions)
        {
            logger.Information("Adding field {num} to {targetPlanId}. Isocenter coordinates [mm]: {@isocenter}; Jaw position [mm]: {@jawPosition}",
                               targetPlan.Beams.Count() + 1,
                               targetPlan.Id,
                               isocenter,
                               jawPositions);
        }

        public static void SetIsocentersLower(this ExternalPlanSetup targetPlan, ExternalPlanSetup sourcePlan)
        {
            targetPlan.ClearBeams();

            StructureSet lowerSS = targetPlan.StructureSet;
            Structure lowerPTVTotal = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_TOTAL);
            DefineIsocentersAndJaws(lowerPTVTotal,
                                    out List<VVector> isoPositions,
                                    out List<Tuple<VRect<double>, VRect<double>>> jawPositions);

            Beam beamSourcePlan = sourcePlan.Beams.FirstOrDefault(b => !b.IsSetupField);
            ExternalBeamMachineParameters sourcePlanBeamParams = new ExternalBeamMachineParameters(beamSourcePlan.TreatmentUnit.Id,
                                                                                                   beamSourcePlan.EnergyModeDisplayName,
                                                                                                   beamSourcePlan.DoseRate,
                                                                                                   beamSourcePlan.Technique.Id,
                                                                                                   "");
            logger.Information("Using beam machine parameters: {@sourcePlanBeamParams} retrieved from plan {sourcePlan}", sourcePlanBeamParams, sourcePlan.Id);

            for (int i = 0; i < isoPositions.Count(); ++i)
            {
                logger.Information("Adding fields at isocenter {num}. Isocenter coordinates [mm]: {@isocenter}", i + 1, isoPositions[i]);

                logger.Information("Field {first}. Jaw position [mm]: {@jawPosition}", (2 * i) + 1, jawPositions[i].Item1);

                targetPlan.AddArcBeam(
                    sourcePlanBeamParams,
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
                    sourcePlanBeamParams,
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

        private static void ClearBeams(this ExternalPlanSetup targetPlan)
        {
            foreach (Beam beam in targetPlan.Beams.ToList()) // avoid Collection modified exception
            {
                logger.Information("Removing existing beam: {beamID}", beam.Id);
                targetPlan.RemoveBeam(beam);
            }
        }

        private static void DefineIsocentersAndJaws(Structure lowerPTVTotal,
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

        public static void CopyCaudalIsocenter(this ExternalPlanSetup targetPlan,
                                               ExternalPlanSetup sourcePlan,
                                               Registration registration)
        {
            List<Beam> sourcePlanBeams = sourcePlan.Beams.Where(b => !b.IsSetupField).ToList();
            double minIsoPos = sourcePlanBeams.First().IsocenterPosition.z;
            foreach (Beam beam in sourcePlanBeams.Skip(1))
            {
                if (beam.IsocenterPosition.z < minIsoPos) minIsoPos = beam.IsocenterPosition.z;
            }

            IEnumerable<Beam> beamsCaudalIso = sourcePlanBeams.Where(beam => beam.IsocenterPosition.z == minIsoPos);

            foreach (Beam beam in beamsCaudalIso)
            {
                VVector beamIso = beam.IsocenterPosition;
                VVector transformedIso = sourcePlan.StructureSet.Image.FOR == registration.SourceFOR
                    ? registration.TransformPoint(beamIso)
                    : registration.InverseTransformPoint(beamIso);

                BeamParameters beamParams = beam.GetEditableParameters();
                List<ControlPointParameters> cpParams = beamParams.ControlPoints.ToList();

                double collAngle = cpParams.First().CollimatorAngle;
                double transformedCollAngle = collAngle > 180 ? collAngle - 180 : 180 + collAngle;
                double transformedGantryAngle = cpParams.Last().GantryAngle;
                double transformedGantryStop = cpParams.First().GantryAngle;

                Beam newBeam = targetPlan.AddVMATBeam(
                    new ExternalBeamMachineParameters(beam.TreatmentUnit.Id, beam.EnergyModeDisplayName, beam.DoseRate, beam.Technique.Id, ""),
                    cpParams.Select(cp => cp.MetersetWeight),
                    transformedCollAngle,
                    transformedGantryAngle,
                    transformedGantryStop,
                    beamParams.GantryDirection == GantryDirection.Clockwise ? GantryDirection.CounterClockwise : GantryDirection.Clockwise,
                    beam.ControlPoints.First().PatientSupportAngle,
                    transformedIso
                );

                newBeam.Id = beam.Id;

                BeamParameters newBeamParams = newBeam.GetEditableParameters();
                /* ISSUE: CalculateDoseWithPresetValues does not work for VMAT
                 * set expected MUs with weight factor
                 */
                newBeamParams.WeightFactor = beam.WeightFactor;
                List<ControlPointParameters> newBeamCPParams = newBeamParams.ControlPoints.ToList();
                foreach (ControlPointParameters cpUpperBeam in cpParams)
                {
                    newBeamCPParams.ElementAt(cpUpperBeam.Index).JawPositions = cpUpperBeam.JawPositions;
                    newBeamCPParams.ElementAt(cpUpperBeam.Index).LeafPositions = cpUpperBeam.LeafPositions;
#if ESAPI18
                    newBeamCPParams.ElementAt(cpParams.Count() - 1 - cpUpperBeam.Index).GantryAngle = cpUpperBeam.GantryAngle;
#endif
                }
                newBeam.ApplyParameters(newBeamParams);
                logger.Information("Caudal field {beam} copied to lower dose-base plan", newBeam.Id);
            }
        }
    }
}