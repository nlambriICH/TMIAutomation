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
        private static string DOSE_PER_FRACTION_GY;
        private static string NUMBER_OF_FRACTIONS;

        public static void OptimizationSetup(this ExternalPlanSetup externalPlanSetup)
        {
            foreach (var line in File.ReadLines("OptimizationOptions.txt").Skip(3))
            {
                string[] optSetup = line.Split('\t');
                OPTIMIZATION_ALGORITHM = optSetup[0];
                DOSE_CALCULATION_ALGORITHM = optSetup[1];
                MLCID = optSetup[2];
                DOSE_PER_FRACTION_GY = optSetup[3];
                NUMBER_OF_FRACTIONS = optSetup[4];
            }

            externalPlanSetup.SetCalculationModel(CalculationType.PhotonVMATOptimization, OPTIMIZATION_ALGORITHM);
            externalPlanSetup.SetCalculationOption(OPTIMIZATION_ALGORITHM, "/PhotonOptCalculationOptions/@MRLevelAtRestart", "MR3"); // Calculation options: \\machinename\dcf$\client
            externalPlanSetup.SetCalculationModel(CalculationType.PhotonVolumeDose, DOSE_CALCULATION_ALGORITHM);
            externalPlanSetup.SetPrescription(int.Parse(NUMBER_OF_FRACTIONS), new DoseValue(double.Parse(DOSE_PER_FRACTION_GY), DoseValue.DoseUnit.Gy), 1.0);
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
