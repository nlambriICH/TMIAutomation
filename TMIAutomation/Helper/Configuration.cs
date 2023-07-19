using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Serilog;

namespace TMIAutomation
{
    public static class Configuration
    {
        private static readonly ILogger logger = Log.ForContext(typeof(Configuration));
        private readonly static Dictionary<string, string> settings = InitializeSettings();

        private static Dictionary<string, string> InitializeSettings()
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string optOptionsPath = Path.Combine(assemblyDir, "Configuration", "OptimizationOptions.txt");
            logger.Verbose("Reading optimization options from {optOptionsPath}", optOptionsPath);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (string line in File.ReadLines(optOptionsPath))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                string[] optSetup = line.Split('\t');
                if (optSetup.Length == 1) continue;
                logger.Verbose("Read parameters: {@optSetup}", optSetup);
                dict.Add(optSetup[0], optSetup[1]);
            }

            return dict;
        }

        public static string OptimizationAlgorithm => settings["OptimizationAlgorithm"];
        public static string DoseAlgorithm => settings["DoseAlgorithm"];
        public static string MLCID => settings["MLCID"];
        public static string DosePerFraction => settings["DosePerFraction"];
        public static string NumberOfFractions => settings["NumberOfFractions"];
        public static string TreatmentMachine => settings["TreatmentMachine"];
    }
}
