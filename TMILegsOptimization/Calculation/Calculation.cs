using Serilog;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMILegsOptimization
{
    static class Calculation
    {
        private static string OPTIMIZATION_ALGORITHM;
        private static string DOSE_CALCULATION_ALGORITHM;
        private static string MLCID;

        public static void SetupModels(this ExternalPlanSetup externalPlanSetup)
        {
            foreach (var line in File.ReadLines("CalculationOptions.txt").Skip(3))
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
            Log.Information("Start optimization for patient {patientId}", patientId);

            OptimizerResult optimizerResult;
            using (var serilogListener = new SerilogTraceListener.SerilogTraceListener())
            {
                Trace.Listeners.Add(serilogListener);
                optimizerResult = externalPlanSetup.OptimizeVMAT(new OptimizationOptionsVMAT(OptimizationIntermediateDoseOption.UseIntermediateDose, MLCID));
            }

            if (!optimizerResult.Success)
            {
                Log.Error("An error occured during optimization");
                return;
            }
            Log.Information("Optimization completed!");
        }
        public static void CalculateDose(this ExternalPlanSetup externalPlanSetup, string patientId)
        {
            Log.Information("Start dose calculation for patient {patientId}", patientId);

            CalculationResult calculationResult;
            using (var serilogListener = new SerilogTraceListener.SerilogTraceListener())
            {
                Trace.Listeners.Add(serilogListener);
                calculationResult = externalPlanSetup.CalculateDose();
            }

            if (!calculationResult.Success)
            {
                Log.Error("An error occured during dose calculation");
                return;
            }
            Log.Information("Dose calculation completed!");
        }
    }
}
