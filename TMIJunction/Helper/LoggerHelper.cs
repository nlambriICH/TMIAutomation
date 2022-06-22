using Serilog;
using System;
using System.IO;
using System.Reflection;
using System.Windows;

namespace TMIJunction
{
    static class LoggerHelper
    {
        public static string LogDirectory
        {
            get
            {
                string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(executingPath, "LOG"));
                return directory.FullName;
            }
        }

        public static void LogAndWarnException(this ILogger logger, Exception exc)
        {
            logger.Error("{@Exception}", exc);
            MessageBox.Show($"Something went wrong: {exc.Message}\n\nFor more details, see the log traces at: {LogDirectory}.", "Error");
        }
    }
}
