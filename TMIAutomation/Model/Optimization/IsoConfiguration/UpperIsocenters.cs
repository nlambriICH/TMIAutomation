using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class UpperIsocenters
    {
        private static readonly ILogger logger = Log.ForContext(typeof(UpperIsocenters));

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
            logger.Information("Using beam machine parameters: {@sourcePlanBeamParams}", sourcePlanBeamParams);
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
                    targetPlan.LogNewBeamInfo(isocenter, jawPositions);

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
                    targetPlan.LogNewBeamInfo(isocenter, jawPositions);
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
                    targetPlan.LogNewBeamInfo(isocenter, jawPositions);
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
                    targetPlan.LogNewBeamInfo(isocenter, jawPositions);
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
                targetPlan.LogNewBeamInfo(isocenterRightArm, jawPositionsRightArm);
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
                targetPlan.LogNewBeamInfo(isocenterLeftArm, jawPositionsLeftArm);
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
                targetPlan.LogNewBeamInfo(isocenterPelivs, jawPositionsPelvis);
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
                targetPlan.LogNewBeamInfo(isocenterPelivs, jawPositionsPelvis);
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
    }
}
