using System;
using System.Diagnostics;
using System.Linq;
using Serilog;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class Calculation
    {
        private static readonly ILogger logger = Log.ForContext(typeof(Calculation));

        public static ExternalPlanSetup AddBaseDosePlan(this Course targetCourse, StructureSet targetSS)
        {
            ExternalPlanSetup newPlan = targetCourse.AddExternalPlanSetup(targetSS);
            int numOfAutoPlans = targetCourse.PlanSetups.Count(p => p.Id.Contains("LowerBase"));
            newPlan.Id = numOfAutoPlans == 0 ? "LowerBase" : string.Concat("LowerBase", numOfAutoPlans);
            logger.Information("Created lower dose-base plan {lowerPlanBase}", newPlan.Id);
            return newPlan;
        }

        public static void SetupOptimization(this ExternalPlanSetup externalPlanSetup)
        {
            externalPlanSetup.SetCalculationModel(CalculationType.PhotonVMATOptimization, Configuration.OptimizationAlgorithm);
            // Calculation options: \\machinename\dcf$\client
#if ESAPI16
            if (!externalPlanSetup.SetCalculationOption(Configuration.OptimizationAlgorithm, "/PhotonOptimizerCalculationOptions/General/OptimizerSettings/@UseGPU", "No"))
            {
                logger.Warning("Could not set UseGPU to No");
            }
            if (!externalPlanSetup.SetCalculationOption(Configuration.OptimizationAlgorithm, "/PhotonOptimizerCalculationOptions/General/AutoFeathering/@AutoFeathering", "Off"))
            {
                logger.Warning("Could not set Autofeathering to Off");
            }
            if (!externalPlanSetup.SetCalculationOption(Configuration.OptimizationAlgorithm, "/PhotonOptimizerCalculationOptions/VMAT/@MRLevelAtRestart", "MR3"))
            {
                logger.Warning("Could not set MR3 level restart for intermediate dose");
            }
#else
            if (!externalPlanSetup.SetCalculationOption(Configuration.OptimizationAlgorithm, "/PhotonOptCalculationOptions/@MRLevelAtRestart", "MR3"))
            {
                logger.Warning("Could not set MR3 level restart for intermediate dose");
            }
            if (!externalPlanSetup.SetCalculationOption(Configuration.OptimizationAlgorithm, "/PhotonOptCalculationOptions/@AutoFeathering", "Off"))
            {
                logger.Warning("Could not set Autofeathering to Off");
            }
#endif
            externalPlanSetup.SetCalculationModel(CalculationType.PhotonVolumeDose, Configuration.DoseAlgorithm);
            externalPlanSetup.SetPrescription(int.Parse(Configuration.NumberOfFractions), new DoseValue(double.Parse(Configuration.DosePerFraction), DoseValue.DoseUnit.Gy), 1.0);
#if ESAPI16
            StringBuilder errorHint = new StringBuilder();
            bool success = externalPlanSetup.SetTargetStructureIfNoDose(externalPlanSetup.StructureSet.Structures.FirstOrDefault(s => s.Id == StructureHelper.LOWER_PTV_NO_JUNCTION),
                                                                        errorHint);
            if (!success)
            {
                logger.Warning($"Could not set target structure {StructureHelper.LOWER_PTV_NO_JUNCTION}.\n{errorHint}");
            }
#endif
        }

        public static void OptimizePlan(this ExternalPlanSetup externalPlanSetup)
        {
            OptimizerResult optimizerResult;
            using (SerilogTraceListener.SerilogTraceListener serilogListener = new SerilogTraceListener.SerilogTraceListener(logger))
            {
                Trace.Listeners.Add(serilogListener);
                optimizerResult = externalPlanSetup.OptimizeVMAT(new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.UseIntermediateDose, Configuration.MLCID));
            }

            if (optimizerResult.Success)
            {
                logger.Information("Optimization completed successfully");
            }
            else
            {
                logger.Error("An error occured during optimization");
                throw new Exception("Optimization was not successful");
            }
        }

        public static void CalculatePlanDose(this ExternalPlanSetup externalPlanSetup)
        {
            CalculationResult calculationResult;
            using (SerilogTraceListener.SerilogTraceListener serilogListener = new SerilogTraceListener.SerilogTraceListener(logger))
            {
                Trace.Listeners.Add(serilogListener);
                calculationResult = externalPlanSetup.CalculateDose();
            }

            if (calculationResult.Success)
            {
                logger.Information("Dose calculation completed successfully");
            }
            else
            {
                logger.Error("An error occured during dose calculation");
                throw new Exception("Dose calculation was not successful");
            }
        }

        public static void ContinueOptimization(this ExternalPlanSetup externalPlanSetup)
        {
            OptimizerResult optimizerResult;
            using (SerilogTraceListener.SerilogTraceListener serilogListener = new SerilogTraceListener.SerilogTraceListener(logger))
            {
                Trace.Listeners.Add(serilogListener);
                optimizerResult = externalPlanSetup.OptimizeVMAT(new OptimizationOptionsVMAT(OptimizationOption.ContinueOptimizationWithPlanDoseAsIntermediateDose, Configuration.MLCID));
            }

            if (optimizerResult.Success)
            {
                logger.Information("Optimization completed successfully");
            }
            else
            {
                logger.Error("An error occured during additional optimization cycle");
                throw new Exception("Optimization was not successful");
            }
        }
    }
}