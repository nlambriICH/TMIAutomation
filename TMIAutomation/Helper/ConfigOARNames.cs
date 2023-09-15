using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace TMIAutomation
{
    public static class ConfigOARNames
    {
        private static readonly ILogger logger = Log.ForContext(typeof(ConfigOARNames));
        private static readonly string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static readonly List<string> oarNames = InitOARNames();

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

        public static List<string> OarNames => oarNames;
    }
}
