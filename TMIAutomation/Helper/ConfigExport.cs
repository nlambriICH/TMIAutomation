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
    public static class ConfigExport
    {
        private static readonly ILogger logger = Log.ForContext(typeof(ConfigOARNames));
        private static readonly string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private readonly static Dictionary<string, string> exportSettings = InitExportSettings();

        private static Dictionary<string, string> InitExportSettings()
        {
            string exportConfigPath = Path.Combine(assemblyDir, "Configuration", "DCMExport.txt");
            logger.Verbose("Reading DICOM export configuration from {exportConfigPath}", exportConfigPath);
            Dictionary<string, string> dict = new Dictionary<string, string>();
            foreach (var line in File.ReadLines(exportConfigPath))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                string[] exportConfig = line.Split('\t');
                if (exportConfig.Length == 1) continue;
                logger.Verbose("Read parameters: {@exportConfig}", exportConfig);
                dict.Add(exportConfig[0], exportConfig[1]);
            }
            return dict;
        }

        public static string DaemonIP => exportSettings["DaemonIP"];
        public static string DaemonPort => exportSettings["DaemonPort"];
        public static string DaemonAETitle => exportSettings["DaemonAETitle"];
        public static string LocalAETitle => exportSettings.ContainsKey("LocalAETitle") ? exportSettings["LocalAETitle"] : Environment.MachineName;
        public static string LocalPort => exportSettings["LocalPort"];
    }
}
