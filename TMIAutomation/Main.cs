using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using Serilog;
using System.IO;
using System;
using TMIAutomation.Async;
using TMIAutomation.ViewModel;
using TMIAutomation.View;
using System.Windows.Forms;
using System.Reflection;
using TMIAutomation;

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        private readonly ILogger logger;

        public Script()
        {
            string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(executingPath, "LOG"));

            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Verbose()
#else
				.MinimumLevel.Debug()
#endif
                .WriteTo.File(Path.Combine(directory.FullName, "TMIJunction.log"),
                              rollingInterval: RollingInterval.Day,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                              shared: true)
                .CreateLogger();

            this.logger = Log.ForContext<Script>();

            logger.Information("TMIJunction script instance created");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, Window window, ScriptEnvironment environment*/)
        {
            Run(new PluginScriptContext(context));
        }

        public void Run(PluginScriptContext context)
        {
            // The ESAPI worker needs to be created in the main thread
            EsapiWorker esapiWorker = new EsapiWorker(context);

            context.Patient.BeginModifications();

            // Create and show the main window on a separate thread
            ConcurrentStaThreadRunner.Run(() =>
            {
                try
                {
                    MainViewModel viewModel = new MainViewModel(esapiWorker);
                    MainWindow mainWindow = new MainWindow(viewModel);
                    mainWindow.ShowDialog();
                }
                catch (Exception exc)
                {
                    MessageBox.Show(new Form { TopMost = true }, exc.Message, "TMIAutomation - Error");
                    logger.Fatal(exc, "The following fatal error occured during the script execution");
                }
            });
        }
    }
}
