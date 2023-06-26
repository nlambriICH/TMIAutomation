using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Serilog;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class Calculation
    {
        private static string OPTIMIZATION_ALGORITHM;
        private static string DOSE_CALCULATION_ALGORITHM;
        private static string MLCID;
        private static string DOSE_PER_FRACTION_GY;
        private static string NUMBER_OF_FRACTIONS;
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
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string optOptionsPath = Path.Combine(assemblyDir, "Configuration", "OptimizationOptions.txt");
            logger.Verbose("Reading optimization options from {optOptionsPath}", optOptionsPath);
            foreach (string line in File.ReadLines(optOptionsPath).Skip(4))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                string[] optSetup = line.Split('\t');
                logger.Verbose("Read parameters: {@optSetup}", optSetup);

                OPTIMIZATION_ALGORITHM = optSetup[0];
                DOSE_CALCULATION_ALGORITHM = optSetup[1];
                MLCID = optSetup[2];
                DOSE_PER_FRACTION_GY = optSetup[3];
                NUMBER_OF_FRACTIONS = optSetup[4];
            }

            externalPlanSetup.SetCalculationModel(CalculationType.PhotonVMATOptimization, OPTIMIZATION_ALGORITHM);
            // Calculation options: \\machinename\dcf$\client
#if ESAPI16
            if (!externalPlanSetup.SetCalculationOption(OPTIMIZATION_ALGORITHM, "/PhotonOptimizerCalculationOptions/General/OptimizerSettings/@UseGPU", "No"))
            {
                logger.Warning("Could not set UseGPU to No");
            }
            if (!externalPlanSetup.SetCalculationOption(OPTIMIZATION_ALGORITHM, "/PhotonOptimizerCalculationOptions/General/AutoFeathering/@AutoFeathering", "Off"))
            {
                logger.Warning("Could not set Autofeathering to Off");
            }
            if (!externalPlanSetup.SetCalculationOption(OPTIMIZATION_ALGORITHM, "/PhotonOptimizerCalculationOptions/VMAT/@MRLevelAtRestart", "MR3"))
            {
                logger.Warning("Could not set MR3 level restart for intermediate dose");
            }
#else
            if (!externalPlanSetup.SetCalculationOption(OPTIMIZATION_ALGORITHM, "/PhotonOptCalculationOptions/@MRLevelAtRestart", "MR3"))
            {
                logger.Warning("Could not set MR3 level restart for intermediate dose");
            }
            if (!externalPlanSetup.SetCalculationOption(OPTIMIZATION_ALGORITHM, "/PhotonOptCalculationOptions/@AutoFeathering", "Off"))
            {
                logger.Warning("Could not set Autofeathering to Off");
            }
#endif
            externalPlanSetup.SetCalculationModel(CalculationType.PhotonVolumeDose, DOSE_CALCULATION_ALGORITHM);
            externalPlanSetup.SetPrescription(int.Parse(NUMBER_OF_FRACTIONS), new DoseValue(double.Parse(DOSE_PER_FRACTION_GY), DoseValue.DoseUnit.Gy), 1.0);
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
                optimizerResult = externalPlanSetup.OptimizeVMAT(new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.UseIntermediateDose, MLCID));
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
                optimizerResult = externalPlanSetup.OptimizeVMAT(new OptimizationOptionsVMAT(OptimizationOption.ContinueOptimizationWithPlanDoseAsIntermediateDose, MLCID));
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