using Serilog;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public static void SetupOptimization(this ExternalPlanSetup externalPlanSetup)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string optOptionsPath = Path.Combine(assemblyDir, "Configuration", "OptimizationOptions.txt");
            logger.Verbose("Reading optimization options from {optOptionsPath}", optOptionsPath);
            foreach (string line in File.ReadLines(optOptionsPath).Skip(3))
            {
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
            bool calcOptionSuccess = externalPlanSetup.SetCalculationOption(OPTIMIZATION_ALGORITHM, "/PhotonOptCalculationOptions/@MRLevelAtRestart", "MR3");
            if (!calcOptionSuccess)
            {
                logger.Warning("Could not set MR3 level restart for intermediate dose");
            }

            externalPlanSetup.SetCalculationModel(CalculationType.PhotonVolumeDose, DOSE_CALCULATION_ALGORITHM);
            externalPlanSetup.SetPrescription(int.Parse(NUMBER_OF_FRACTIONS), new DoseValue(double.Parse(DOSE_PER_FRACTION_GY), DoseValue.DoseUnit.Gy), 1.0);
        }

        public static bool OptimizePlan(this ExternalPlanSetup externalPlanSetup)
        {
            OptimizerResult optimizerResult;
            using (SerilogTraceListener.SerilogTraceListener serilogListener = new SerilogTraceListener.SerilogTraceListener(logger))
            {
                Trace.Listeners.Add(serilogListener);
                optimizerResult = externalPlanSetup.OptimizeVMAT(new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.UseIntermediateDose, MLCID));
            }

            return optimizerResult.Success;
        }

        public static bool CalculatePlanDose(this ExternalPlanSetup externalPlanSetup)
        {
            CalculationResult calculationResult;
            using (SerilogTraceListener.SerilogTraceListener serilogListener = new SerilogTraceListener.SerilogTraceListener(logger))
            {
                Trace.Listeners.Add(serilogListener);
                calculationResult = externalPlanSetup.CalculateDose();
            }

            return calculationResult.Success;
        }
    }
}
