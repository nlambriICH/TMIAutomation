using Serilog;
using System;
using System.IO;
using System.Windows;

namespace TMIAutomation
{
    static class LoggerHelper
    {
        public static string LogDirectory
        {
            get
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(desktopPath, "TMIAutomation"));
                return directory.FullName;
            }
        }

        public static void LogAndWarnException(Exception exc)
        {
            Log.Error(exc.Message);
            MessageBox.Show($"Something went wrong... see traces at {LogDirectory}", "Error");
        }
    }
}
