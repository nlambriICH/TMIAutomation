using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Serilog;

namespace TMIAutomation
{
    public static class ConfigOptOptions
    {
        private static readonly ILogger logger = Log.ForContext(typeof(ConfigOptOptions));
        private static readonly string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static Dictionary<string, string> optSettings;

        public static void Init()
        {
            optSettings = new Dictionary<string, string>();
            string optOptionsPath = Path.Combine(assemblyDir, "Configuration", "OptimizationOptions.txt");
            logger.Verbose("Reading optimization options from {optOptionsPath}", optOptionsPath);
            foreach (string line in File.ReadLines(optOptionsPath))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                string[] optSetup = line.Split('\t');
                if (optSetup.Length == 1) continue;
                logger.Verbose("Read parameters: {@optSetup}", optSetup);
                optSettings.Add(optSetup[0], optSetup[1]);
            }
        }

        public static string OptimizationAlgorithm => optSettings["OptimizationAlgorithm"];
        public static string DoseAlgorithm => optSettings["DoseAlgorithm"];
        public static string MLCID => optSettings["MLCID"];
        public static string DosePerFraction => optSettings["DosePerFraction"];
        public static string NumberOfFractions => optSettings["NumberOfFractions"];
        public static string TreatmentMachine => optSettings["TreatmentMachine"];
        public static string LowerExtremitiesCollimator => optSettings.ContainsKey("LowerExtremitiesCollimator") ? optSettings["LowerExtremitiesCollimator"] : string.Empty;
        public static bool BaseDosePlanning =>
#if ESAPI15
                optSettings.ContainsKey("BaseDosePlanning") && optSettings["BaseDosePlanning"] == "Yes";
#else
                true; // Always true for ESAPI16 and ESAPI18
        public static bool AutoPlanLowerExtremities => optSettings["AutoPlanLowerExtremities"] == "Yes";
#endif
    }
}
