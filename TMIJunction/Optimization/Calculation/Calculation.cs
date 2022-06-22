using Serilog;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIJunction
{
    static class Calculation
    {
        private static string OPTIMIZATION_ALGORITHM;
        private static string DOSE_CALCULATION_ALGORITHM;
        private static string MLCID;
        private static readonly ILogger logger = Log.ForContext(typeof(Calculation));

        public static void SetupModels(this ExternalPlanSetup externalPlanSetup)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (var line in File.ReadLines(Path.Combine(assemblyDir, "..", "..", "Configuration", "CalculationOptions.txt")).Skip(3))
            {
                string[] calcOptions = line.Split('\t');
                OPTIMIZATION_ALGORITHM = calcOptions[0];
                DOSE_CALCULATION_ALGORITHM = calcOptions[1];
                MLCID = calcOptions[2];
            }

            externalPlanSetup.SetCalculationModel(CalculationType.PhotonVMATOptimization, OPTIMIZATION_ALGORITHM);
            externalPlanSetup.SetCalculationOption(OPTIMIZATION_ALGORITHM, "/PhotonOptCalculationOptions/@MRLevelAtRestart", "MR3"); // Calculation options: \\machinename\dcf$\client
            externalPlanSetup.SetCalculationModel(CalculationType.PhotonVolumeDose, DOSE_CALCULATION_ALGORITHM);
        }

        public static void OptimizePlan(this ExternalPlanSetup externalPlanSetup, string patientId)
        {
            logger.Information("Start optimization for patient {patientId}", patientId);

            OptimizerResult optimizerResult;
            using (var serilogListener = new SerilogTraceListener.SerilogTraceListener(logger))
            {
                Trace.Listeners.Add(serilogListener);
                optimizerResult = externalPlanSetup.OptimizeVMAT(new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.UseIntermediateDose, MLCID));
            }

            if (!optimizerResult.Success)
            {
                logger.Error("An error occured during optimization");
                MessageBox.Show("The optimization was NOT successful!", "Error");
                return;
            }
            logger.Information("Optimization completed!");
            WindowHelper.ShowAutoClosingMessageBox("Optimization completed!", "Info");
        }
        public static void CalculateDose(this ExternalPlanSetup externalPlanSetup, string patientId)
        {
            logger.Information("Start dose calculation for patient {patientId}", patientId);

            CalculationResult calculationResult;
            using (var serilogListener = new SerilogTraceListener.SerilogTraceListener(logger))
            {
                Trace.Listeners.Add(serilogListener);
                calculationResult = externalPlanSetup.CalculateDose();
            }

            if (!calculationResult.Success)
            {
                logger.Error("An error occured during dose calculation");
                MessageBox.Show("The dose calculation was NOT successful!", "Error");
                return;
            }
            logger.Information("Dose calculation completed!");
            WindowHelper.ShowAutoClosingMessageBox("Dose calculation completed!", "Info");
        }
    }
}
