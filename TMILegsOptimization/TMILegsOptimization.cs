using System;
using System.IO;
using System.Linq;
using System.Reflection;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
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
        private static string OPTIMIZATION_ALGORITHM;
        private static string DOSE_CALCULATION_ALGORITHM;
        private static string MLCID;

        private static void Init()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            DirectoryInfo directory = Directory.CreateDirectory(Path.Combine(desktopPath, "TMIAutomation"));

            Log.Logger = new LoggerConfiguration()
                .WriteTo.File(Path.Combine(directory.FullName, "TMILegsOptimization.log"),
                              rollingInterval: RollingInterval.Day,
                              outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.Console(theme: AnsiConsoleTheme.Literate)
                .CreateLogger();

            foreach (var line in File.ReadLines("CalculationOptions.txt").Skip(3))
            {
                string[] calcOptions = line.Split('\t');
                OPTIMIZATION_ALGORITHM = calcOptions[0];
                DOSE_CALCULATION_ALGORITHM = calcOptions[1];
                MLCID = calcOptions[2];
            }

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
                }
            }
            catch (Exception e)
            {
                Log.Error("{@Exception}", e);
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

                ClearObjectives(optSetup);
                AddPointObjectives(ss, optSetup);
                AddEUDObjectives(ss, optSetup);
                optSetup.UseJawTracking = false;
                optSetup.AddAutomaticNormalTissueObjective(150);

                ExternalPlanSetup externalPlanSetup = patient.Courses.FirstOrDefault(c => c.Id == courseId).ExternalPlanSetups.FirstOrDefault(ps => ps.Id == planId);
                OptimizePlan(patientId, planSetup, externalPlanSetup);

                FitYJawToMLC(externalPlanSetup);

                CalculateDose(patientId, planSetup, externalPlanSetup);

                app.SaveModifications();
                app.ClosePatient();
            }
        }

        private static void FitYJawToMLC(ExternalPlanSetup externalPlanSetup)
        {
            foreach (Beam beam in externalPlanSetup.Beams)
            {
                Log.Information($"FitYJawToMLC for beam {beam.Id}");
                double minLeafGap = beam.MLC.MinDoseDynamicLeafGap;

                int numLeafClosedY1 = 60;
                int numLeafClosedY2 = 60;
                foreach (ControlPoint cp in beam.ControlPoints)
                {
                    VRect<double> jawPositionsCP = cp.JawPositions;
                    float[,] leafPositions = cp.LeafPositions;

                    int numLeafClosedY1CP = 0;
                    for (int i = 0; i < leafPositions.GetLength(1); ++i)
                    {
                        if (Math.Abs(leafPositions[0, i] - leafPositions[1, i]) < minLeafGap)
                        {
                            ++numLeafClosedY1CP;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (numLeafClosedY1CP < numLeafClosedY1) numLeafClosedY1 = numLeafClosedY1CP;

                    int numLeafClosedY2CP = 0;
                    for (int i = leafPositions.GetLength(1) - 1; i >= 0; --i)
                    {
                        if (Math.Abs(leafPositions[0, i] - leafPositions[1, i]) < minLeafGap)
                        {
                            ++numLeafClosedY2CP;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (numLeafClosedY2CP < numLeafClosedY2) numLeafClosedY2 = numLeafClosedY2CP;
                }

                BeamParameters beamParameters = beam.GetEditableParameters();
                double maxJawY = 200;
                foreach (ControlPointParameters cpParams in beamParameters.ControlPoints)
                {
                    VRect<double> jawPositions = cpParams.JawPositions;
                    VRect<double> newJawPos = new VRect<double>(jawPositions.X1, numLeafClosedY1 * 10 - maxJawY, jawPositions.X2, maxJawY - numLeafClosedY2 * 10);
                    cpParams.JawPositions = newJawPos;
                }
                beam.ApplyParameters(beamParameters);
            }
        }

        private static void CalculateDose(string patientId, PlanSetup planSetup, ExternalPlanSetup externalPlanSetup)
        {
            planSetup.SetCalculationModel(CalculationType.PhotonVolumeDose, DOSE_CALCULATION_ALGORITHM);
            Log.Information($"Start dose calculation for patient {patientId}");
            CalculationResult calculationResult = externalPlanSetup.CalculateDose();
            if (!calculationResult.Success)
            {
                throw new Exception("An error occured during dose calculation");
            }
            Log.Information($"Dose calculation completed!");
        }

        private static void OptimizePlan(string patientId, PlanSetup planSetup, ExternalPlanSetup externalPlanSetup)
        {
            planSetup.SetCalculationModel(CalculationType.PhotonVMATOptimization, OPTIMIZATION_ALGORITHM);
            Log.Information($"Start optimization for patient {patientId}");
            OptimizerResult optimizerResult = externalPlanSetup.OptimizeVMAT(new OptimizationOptionsVMAT(OptimizationOption.RestartOptimization, MLCID));
            if (!optimizerResult.Success)
            {
                throw new Exception("An error occured during optimization");
            }
            Log.Information($"Optimization completed!");
        }

        private static void ClearObjectives(OptimizationSetup optSetup)
        {
            foreach (var objective in optSetup.Objectives)
            {
                optSetup.RemoveObjective(objective);
            }
        }

        private static void AddPointObjectives(StructureSet ss, OptimizationSetup optSetup)
        {
            foreach (var line in File.ReadLines("PointOptimizationObjectives.txt").Skip(3))
            {
                string[] pointObjectiveParams = line.Split('\t');
                Structure structure = ss.Structures.FirstOrDefault(s => s.Id == pointObjectiveParams[0]);
                var limit = (OptimizationObjectiveOperator) Enum.Parse(typeof(OptimizationObjectiveOperator), pointObjectiveParams[1], true);
                if (!double.TryParse(pointObjectiveParams[2], out double volume)
                    || !double.TryParse(pointObjectiveParams[3], out double doseValue)
                    || !double.TryParse(pointObjectiveParams[5], out double priority))
                {
                    Log.Error($"Fail parsing PointObjectives: {line}");
                    continue;
                }

                string doseUnit = pointObjectiveParams[4];

                optSetup.AddPointObjective(structure, limit, new DoseValue(doseValue, doseUnit), volume, priority);
            }
        }

        private static void AddEUDObjectives(StructureSet ss, OptimizationSetup optSetup)
        {
            foreach (var line in File.ReadLines("EUDOptimizationObjectives.txt").Skip(3))
            {
                string[] eudObjectiveParams = line.Split('\t');
                Structure structure = ss.Structures.FirstOrDefault(s => s.Id == eudObjectiveParams[0]);
                var limit = (OptimizationObjectiveOperator)Enum.Parse(typeof(OptimizationObjectiveOperator), eudObjectiveParams[1], true);
                if (!double.TryParse(eudObjectiveParams[2], out double doseValue)
                    || !double.TryParse(eudObjectiveParams[4], out double priority)
                    || !double.TryParse(eudObjectiveParams[5], out double gEUDa))
                {
                    Log.Error($"Fail parsing EUDObjectives: {line}");
                    continue;
                }
                
                string doseUnit = eudObjectiveParams[3];

                optSetup.AddEUDObjective(structure, limit, new DoseValue(doseValue, doseUnit), gEUDa, priority);
            }
        }

    }
}
