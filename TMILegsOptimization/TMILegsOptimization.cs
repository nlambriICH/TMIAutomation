using System;
using System.IO;
using System.Linq;
using System.Reflection;
using VMS.TPS.Common.Model.API;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using VMS.TPS.Common.Model.Types;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.3")]
[assembly: AssemblyFileVersion("1.0.0.3")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
[assembly: ESAPIScript(IsWriteable = true)]

namespace TMILegsOptimization
{
    class Program
    {
        private static void Init()
        {
            string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(executingPath, "LOG"));

            Log.Logger = new LoggerConfiguration()
                .Destructure.ByTransforming<VVector>(vv => new { X = vv.x, Y = vv.y, Z = vv.z })
                .Destructure.ByTransforming<VRect<double>>(vr => new { vr.X1, vr.X2, vr.Y1, vr.Y2 })
                .MinimumLevel.Debug()
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
                bool createNewPlan = info.Length > 3 && bool.Parse(info[3]);
                string machineName = info.Length > 4 ? info[4] : string.Empty;

                Patient patient = app.OpenPatientById(patientId);
                Course course = patient.Courses.FirstOrDefault(c => c.Id == courseId);
                ExternalPlanSetup externalPlanSetup = course.ExternalPlanSetups.FirstOrDefault(ps => ps.Id == planId);
                StructureSet ss = externalPlanSetup.StructureSet;

                patient.BeginModifications();

                if (createNewPlan)
                {
                    ExternalPlanSetup newPlan = course.AddExternalPlanSetup(ss);
                    int numOfAutoPlans = course.PlanSetups.Count(ps => ps.Id.Contains("AutoOpt"));
                    newPlan.Id = numOfAutoPlans == 0 ? "AutoOpt" : string.Concat("AutoOpt", numOfAutoPlans);

                    Log.Information("Create new plan {planId} in course {courseId} for patient {patientId}", newPlan.Id, courseId, patientId);

                    Beam beam = externalPlanSetup.Beams.First();
                    string machineNameOldPlan = beam.TreatmentUnit.Id;
                    string energy = beam.EnergyModeDisplayName;
                    int doseRate = beam.DoseRate;
                    string technique = beam.Technique.Id;
                    newPlan.SetFields(externalPlanSetup.TargetVolumeID, machineNameOldPlan, energy, doseRate, technique);
                }
                else
                {
                    externalPlanSetup.SetFields("PTV_Total", machineName, "6X", 600, "ARC"); // assume structure names as those created by the plug-in script
                }

                externalPlanSetup.OptimizationSetup(); // must set dose prescription before adding objectives

                OptimizationSetup optSetup = externalPlanSetup.OptimizationSetup;

                optSetup.ClearObjectives();
                optSetup.AddPointObjectives(ss);
                optSetup.AddEUDObjectives(ss);
                optSetup.UseJawTracking = false;
                optSetup.AddAutomaticNormalTissueObjective(150);
                //optSetup.ExcludeStructuresFromOptimization(ss);

                externalPlanSetup.OptimizePlan(patientId);
                externalPlanSetup.AdjustYJawToMLCShape();
                externalPlanSetup.CalculateDose(patientId);
                externalPlanSetup.Normalize();

                //app.SaveModifications();
                app.ClosePatient();
            }
        }
    }
}
