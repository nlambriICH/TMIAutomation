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
        private static readonly string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly Dictionary<string, string> optSettings = InitOptSettings();
        private static readonly List<string> oarNames = InitOARNames();

        private static Dictionary<string, string> InitOptSettings()
        {
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

        private static List<string> InitOARNames()
        {
            string oarNamesPath = Path.Combine(assemblyDir, "Configuration", "OARNames.txt");
            logger.Verbose("Reading OAR names from {oarNamesPath}", oarNamesPath);
            List<string> list = new List<string>();
            foreach (string line in File.ReadLines(oarNamesPath))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                logger.Verbose("Read name: {line}", line);
                list.Add(line);
            }

            return list;
        }

        public static string OptimizationAlgorithm => optSettings["OptimizationAlgorithm"];
        public static string DoseAlgorithm => optSettings["DoseAlgorithm"];
        public static string MLCID => optSettings["MLCID"];
        public static string DosePerFraction => optSettings["DosePerFraction"];
        public static string NumberOfFractions => optSettings["NumberOfFractions"];
        public static string TreatmentMachine => optSettings["TreatmentMachine"];
        public static List<string> OarNames => oarNames;
    }
}
