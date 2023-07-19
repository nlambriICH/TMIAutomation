using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Serilog;
using TMIAutomation;
using TMIAutomation.Async;
using TMIAutomation.View;
using TMIAutomation.ViewModel;
using VMS.TPS.Common.Model.API;

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        private readonly ILogger logger;
        private readonly string logPath;

        public Script()
        {
            string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(executingPath, "LOG"));
            this.logPath = directory.FullName;

            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Verbose()
#else
                .MinimumLevel.Debug()
#endif
                .WriteTo.File(Path.Combine(logPath, "TMIJunction.log"),
                              rollingInterval: RollingInterval.Day,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                              shared: true)
                .CreateLogger();

            this.logger = Log.ForContext<Script>();

            logger.Verbose("TMIJunction script instance created");
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

            logger.Information("TMIJunction script context patient: {lastName}, {firstName} ({patientId})",
                               context.Patient.LastName,
                               context.Patient.FirstName,
                               context.Patient.Id
                               );
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
                    string msgBoxMessage = exc.Message +
                    $"\n\nFor more information, please check the logs generated at: {this.logPath}";
                    MessageBox.Show(new Form { TopMost = true }, msgBoxMessage, "TMIAutomation - Error");
                    logger.Fatal(exc, "The following fatal error occured during the script execution");
                }
            });
        }
    }
}