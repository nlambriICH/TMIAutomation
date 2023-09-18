using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Serilog;

namespace TMIAutomation
{
    public static class ConfigOARNames
    {
        private static readonly ILogger logger = Log.ForContext(typeof(ConfigOARNames));
        private static readonly string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public static void Init()
        {
            OarNames = new List<string>();
            string oarNamesPath = Path.Combine(assemblyDir, "Configuration", "OARNames.txt");
            logger.Verbose("Reading OAR names from {oarNamesPath}", oarNamesPath);
            foreach (string line in File.ReadLines(oarNamesPath))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                logger.Verbose("Read name: {line}", line);
                OarNames.Add(line);
            }
        }

        public static List<string> OarNames { get; private set; }
    }
}
