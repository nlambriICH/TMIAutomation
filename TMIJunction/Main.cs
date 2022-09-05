using TMIJunction;
using System.Runtime.CompilerServices;
using VMS.TPS.Common.Model.API;
using Serilog;
using System.IO;
using System;
using VMS.TPS.Common.Model.Types;
using TMIJunction.Async;
using TMIJunction.ViewModel;
using TMIJunction.View;
using System.Windows.Forms;

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
                try
                {
                    MainViewModel viewModel = new MainViewModel(esapiWorker);
                    MainWindow mainWindow = new MainWindow(viewModel);
                    mainWindow.ShowDialog();
                    mainWindow.Closed += CloseAndFlushLogger;
                }
                catch (Exception exc)
                {
                    MessageBox.Show(new Form { TopMost = true }, exc.Message, "TMIAutomation - Error");
                }
            });

        }
        public void CloseAndFlushLogger(object sender, EventArgs e)
        {
            Log.CloseAndFlush();
        }
    }
}
