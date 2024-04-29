using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Serilog;

namespace TMIAutomation
{
    public static class ConfigExport
    {
        private static readonly ILogger logger = Log.ForContext(typeof(ConfigExport));
        private static readonly string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        private static bool isFirstExecution = true;
        private static Dictionary<string, string> exportSettings;

        public static void Init()
        {
            exportSettings = new Dictionary<string, string>();
            string exportConfigPath = Path.Combine(assemblyDir, "Configuration", "DCMExport.txt");
            logger.Verbose("Reading DICOM export configuration from {exportConfigPath}", exportConfigPath);
            foreach (var line in File.ReadLines(exportConfigPath))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                string[] exportConfig = line.Split('\t');
                if (exportConfig.Length == 1) continue;
                logger.Verbose("Read parameters: {@exportConfig}", exportConfig);
                exportSettings.Add(exportConfig[0], exportConfig[1]);
            }

            if (isFirstExecution)
            {
                DICOMServices.Init();
                DICOMServices.CreateSCP();
                Directory.CreateDirectory(DICOMStorage);
                isFirstExecution = false;
            }
        }

        public static string ExportType => exportSettings["ExportType"];
        public static string DaemonAETitle => exportSettings["DaemonAETitle"];
        public static string DaemonIP => exportSettings["DaemonIP"];
        public static string DaemonPort => exportSettings["DaemonPort"];
        public static string LocalAETitle => exportSettings.ContainsKey("LocalAETitle") ? exportSettings["LocalAETitle"] : Environment.MachineName;
        public static string LocalPort => exportSettings["LocalPort"];
        public static string DICOMStorage { get; } = Path.Combine(assemblyDir, "Dicoms");
    }
}
