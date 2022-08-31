using TMIJunction;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using VMS.TPS.Common.Model.API;
using Serilog;
using System.IO;
using System;
using System.Linq;
using VMS.TPS.Common.Model.Types;
using TMIJunction.Async;
using TMIJunction.ViewModel;
using TMIJunction.View;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.8")]
[assembly: AssemblyFileVersion("1.0.0.8")]
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
                .MinimumLevel.Debug()
                .Destructure.ByTransforming<VVector>(vv => new { X = vv.x, Y = vv.y, Z = vv.z })
                .Destructure.ByTransforming<VRect<double>>(vr => new { vr.X1, vr.X2, vr.Y1, vr.Y2 })
                .WriteTo.File(Path.Combine(LoggerHelper.LogDirectory, "TMIJunction.log"),
                              rollingInterval: RollingInterval.Day,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            this.logger = Log.ForContext<Script>();

            logger.Information("TMIJunction script instance created");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context /*, Window window, ScriptEnvironment environment*/)
        {

            // The ESAPI worker needs to be created in the main thread
            EsapiWorker esapiWorker = new EsapiWorker(context);

            context.Patient.BeginModifications();

            // Create and show the main window on a separate thread
            ConcurrentStaThreadRunner.Run(() =>
            {
                LegsJunction legsJunction = new LegsJunction(esapiWorker);
                MainViewModel viewModel = new MainViewModel(esapiWorker, legsJunction);
                MainWindow mainWindow = new MainWindow(viewModel);
                mainWindow.ShowDialog();
                mainWindow.Closed += CloseAndFlushLogger;
            });

            //UserInterface ui = new UserInterface(context)
            //{
            //    DataContext = new UserInterfaceModel(context.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last())
            //};

            //window.Content = ui;
            //window.SizeToContent = SizeToContent.WidthAndHeight;
            //window.ResizeMode = ResizeMode.NoResize;
            //window.Title = "ESAPI";

            //logger.Information("Window content set to the user interface");

            //window.Closed += CloseAndFlushLogger;

        }
        public void CloseAndFlushLogger(object sender, EventArgs e)
        {
            Log.CloseAndFlush();
        }
    }
}
