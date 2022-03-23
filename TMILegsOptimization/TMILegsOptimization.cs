using System;
using System.IO;
using System.Linq;
using System.Reflection;
using VMS.TPS.Common.Model.API;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
[assembly: ESAPIScript(IsWriteable = true)]

namespace TMILegsOptimization
{
    class Program
    {
        private static void Init()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(desktopPath, "TMIAutomation"));

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(Path.Combine(directory.FullName, "TMILegsOptimization.log"),
                              rollingInterval: RollingInterval.Day,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
                .CreateLogger();
        }

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                using (Application app = Application.CreateApplication())
                {
                    Init();
                    Execute(app);
                    Console.WriteLine("Press any key to close...");
                    Console.ReadLine();
                }
            }
            catch (Exception e)
            {
                Log.Fatal("{@Exception}", e);
                Console.WriteLine("Press any key to close...");
                Console.ReadLine();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        static void Execute(Application app)
        {

            foreach (var infoLine in File.ReadLines("PatientsInfo.txt").Skip(3))
            {
                string[] info = infoLine.Split('\t');
                string patientId = info[0];
                string courseId = info[1];
                string planId = info[2];

                Patient patient = app.OpenPatientById(patientId);
                PlanSetup planSetup = patient.Courses.FirstOrDefault(c => c.Id == courseId).PlanSetups.FirstOrDefault(ps => ps.Id == planId);
                StructureSet ss = planSetup.StructureSet;

                OptimizationSetup optSetup = planSetup.OptimizationSetup;

                patient.BeginModifications();

                optSetup.ClearObjectives();
                optSetup.AddPointObjectives(ss);
                optSetup.AddEUDObjectives(ss);
                optSetup.UseJawTracking = false;
                optSetup.AddAutomaticNormalTissueObjective(150);
                //optSetup.ExcludeStructuresFromOptimization(ss);

                ExternalPlanSetup externalPlanSetup = patient.Courses.FirstOrDefault(c => c.Id == courseId).ExternalPlanSetups.FirstOrDefault(ps => ps.Id == planId);

                externalPlanSetup.SetupModels();
                externalPlanSetup.OptimizePlan(patientId);
                externalPlanSetup.AdjustYJawToMLCShape();
                externalPlanSetup.CalculateDose(patientId);
                externalPlanSetup.Normalize();

                app.SaveModifications();
                app.ClosePatient();
            }
        }
    }
}
