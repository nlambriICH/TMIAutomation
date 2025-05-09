﻿using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Serilog;
using TMIAutomation;
using TMIAutomation.Async;
using TMIAutomation.View;
using TMIAutomation.ViewModel;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {
        private readonly ILogger logger;
        private readonly string logPath;
        private readonly string executingPath;
        private Process serverProcess;

        public Script()
        {
            this.executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(this.executingPath, "LOG"));
            this.logPath = directory.FullName;

            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Verbose()
#else
                .MinimumLevel.Debug()
#endif
                .WriteTo.File(Path.Combine(logPath, "TMIAutomation.log"),
                              rollingInterval: RollingInterval.Day,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                              shared: true)
                .CreateLogger();

            this.logger = Log.ForContext<Script>();

            logger.Verbose("TMIAutomation script instance created");

            // Load settings to avoid null refs when static members are called from UI thread 
            ConfigExport.Init();
            ConfigOARNames.Init();
            ConfigOptOptions.Init();
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

            logger.Information("TMIAutomation script context patient: {lastName}, {firstName} ({patientId})",
                               context.Patient.LastName,
                               context.Patient.FirstName,
                               context.Patient.Id
                               );

            bool schedule = false;
            foreach (Course course in context.Patient.Courses)
            {
                var upperBodyPlans = course.PlanSetups.Where(ps => ps.IsDoseValid && ps.StructureSet.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine);
                var lowerExtremitiesPlans = course.PlanSetups.Where(ps => ps.IsDoseValid && ps.StructureSet.Image.ImagingOrientation == PatientOrientation.FeetFirstSupine);
                schedule = upperBodyPlans.Any() && lowerExtremitiesPlans.Any();
                if (schedule) break;
            }
            bool feetFirstSupine = context.Image?.ImagingOrientation == PatientOrientation.FeetFirstSupine; // false if image == null

            context.Patient.BeginModifications();

            // Create and show the main window on a separate thread
            ConcurrentStaThreadRunner.Run(() =>
            {
                try
                {
                    this.StartLocalServer();

                    MainViewModel viewModel = new MainViewModel(esapiWorker);
                    MainWindow mainWindow = new MainWindow(viewModel);
                    mainWindow.ScheduleTabItem.IsSelected = schedule;
                    mainWindow.LowerTabItem.IsSelected = !schedule && feetFirstSupine;
                    mainWindow.UpperTabItem.IsSelected = !schedule && !feetFirstSupine;
                    mainWindow.ShowDialog();
                }
                catch (Exception exc)
                {
                    string msgBoxMessage = exc.Message +
                    $"\n\nFor more information, please check the logs generated at: {this.logPath}";
                    MessageBox.Show(new Form { TopMost = true }, msgBoxMessage, "TMIAutomation - Error");
                    logger.Fatal(exc, "The following fatal error occured during the script execution");
                }
                finally
                {
                    this.serverProcess?.Kill();
                }
            });
        }

        private void StartLocalServer()
        {
            try
            {
                string serverDirectory = Path.Combine(this.executingPath, "dist", "app");
                string serverPath = Path.Combine(serverDirectory, "app.exe");
                ProcessStartInfo startInfo = new ProcessStartInfo(serverPath)
                {
                    WorkingDirectory = serverDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                this.serverProcess = Process.Start(startInfo);
            }
            catch (Exception e)
            {
                logger.Error("Could not start the local server", e);
            }
        }
    }
}