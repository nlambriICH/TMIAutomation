using System;
using System.Linq;
using Serilog;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class PostCalculation
    {
        private static readonly ILogger logger = Log.ForContext(typeof(PostOptimization));

        public static void Normalize(this ExternalPlanSetup externalPlanSetup, DoseValue doseValueNormTarget, double volumeNormalizationTarget)
        {
            Structure lowerPTVNoJ = externalPlanSetup.StructureSet.Structures.FirstOrDefault(s => s.Id == StructureHelper.LOWER_PTV_NO_JUNCTION);
            if (lowerPTVNoJ == null)
            {
                logger.Warning("Could not find structure: {ptvTotNoJunction}. Skip plan normalization", StructureHelper.LOWER_PTV_NO_JUNCTION);
                return;
            }

            externalPlanSetup.PlanNormalizationValue = 100.0; // no plan normalization

            double volume = externalPlanSetup.GetVolumeAtDose(lowerPTVNoJ, doseValueNormTarget, VolumePresentation.Relative);

            DoseValue doseValue = externalPlanSetup.GetDoseAtVolume(lowerPTVNoJ, volumeNormalizationTarget, VolumePresentation.Relative, DoseValuePresentation.Absolute);
            externalPlanSetup.PlanNormalizationValue = 100.0 * doseValue.Dose / doseValueNormTarget.Dose;
        }

        public static bool NeedAdditionalOptimizationCycle(this PlanningItem planningItem, OptimizationCycleTarget optCycleTarget)
        {
            DoseValue totalDose;
            switch (planningItem)
            {
                case ExternalPlanSetup externalPlanSetup:
                    externalPlanSetup.DoseValuePresentation = DoseValuePresentation.Absolute;
                    totalDose = externalPlanSetup.TotalDose;
                    break;
                case PlanSum planSum:
                    PlanSetup planSetup = planSum.PlanSetups.FirstOrDefault();
                    planSetup.DoseValuePresentation = DoseValuePresentation.Absolute;
                    totalDose = planSetup.TotalDose;
                    break;
                default:
                    totalDose = new DoseValue();
                    break;
            }

            switch (optCycleTarget)
            {
                case OptimizationCycleTarget.LowerPTVNoJ:
                    Structure lowerPTVNoJ = planningItem.StructureSet.Structures.FirstOrDefault(s => s.Id == StructureHelper.LOWER_PTV_NO_JUNCTION);
                    DoseValue meanDose = planningItem.GetDVHCumulativeData(lowerPTVNoJ,
                                                                          DoseValuePresentation.Relative,
                                                                          VolumePresentation.Relative,
                                                                          0.001).MeanDose;
                    if (meanDose.Dose > 105.0)
                    {
                        logger.Information("Mean dose was {meanDose} (>105%). Performing additional optimization cycle",
                                           Math.Round(meanDose.Dose, 2, MidpointRounding.AwayFromZero));
                        return true;
                    }
                    else
                    {
                        logger.Information("Mean dose was {meanDose} (<=105%). Skip additional optimization cycle",
                                           Math.Round(meanDose.Dose, 2, MidpointRounding.AwayFromZero));
                        return false;
                    }
                case OptimizationCycleTarget.LowerPTV_J:
                    Structure lowerPTVJ = planningItem.StructureSet.Structures.FirstOrDefault(s => s.Id == StructureHelper.LOWER_PTV_JUNCTION);
                    double lowerPTVJVolumeCoverage = planningItem.GetVolumeAtDose(lowerPTVJ,
                                                                                 totalDose,
                                                                                 VolumePresentation.Relative);
                    if (lowerPTVJVolumeCoverage < 98.0)
                    {
                        logger.Information("V100%-{lowerPTVJId} was {lowerPTVJVolumeCoverage} (<98%). Performing additional optimization cycle",
                                           lowerPTVJ.Id,
                                           Math.Round(lowerPTVJVolumeCoverage, 2, MidpointRounding.AwayFromZero));
                        return true;
                    }
                    else
                    {
                        logger.Information("V100%-{lowerPTVJId} was {lowerPTVJVolumeCoverage} (>=98%). Skip additional optimization cycle",
                                           lowerPTVJ.Id,
                                           Math.Round(lowerPTVJVolumeCoverage, 2, MidpointRounding.AwayFromZero));
                        return false;
                    }
                default:
                    return false;
            }
        }
    }
}