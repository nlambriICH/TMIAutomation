using TMIJunction;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using VMS.TPS.Common.Model.API;
using Serilog;
using System.IO;
using System;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.6")]
[assembly: AssemblyFileVersion("1.0.0.6")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {

        private readonly ILogger logger;

        public Script()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(LoggerHelper.LogDirectory, "TMIJunction.log"),
                              rollingInterval: RollingInterval.Day,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            this.logger = Log.ForContext<Script>();

            logger.Information("TMIJunction script instance created");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context, Window window /*, ScriptEnvironment environment*/)
        {
            UserInterface ui = new UserInterface(context)
            {
                DataContext = new UserInterfaceModel()
            };

            window.Content = ui;
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.ResizeMode = ResizeMode.NoResize;
            window.Title = "ESAPI";

            logger.Information("Window content set to the user interface");

            window.Closed += CloseAndFlushLogger;

        }
        public void CloseAndFlushLogger(object sender, EventArgs e)
        {
            Log.CloseAndFlush();
        }
    }
}
