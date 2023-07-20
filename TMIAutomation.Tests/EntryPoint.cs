using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMIAutomation.Async;
using VMS.TPS.Common.Model.API;

[assembly: ESAPIScript(IsWriteable = true)]

namespace TMIAutomation.Tests
{
    internal class EntryPoint
    {
        private static Application EclipseApp { get; set; }
        private static readonly Dictionary<string, string> testData = InitializeTestData();

        [STAThread]
        public static void Main(string[] args)
        {
            using (EclipseApp = Application.CreateApplication())
            {
                string patientID = testData["PatientID"];
                Patient patient = EclipseApp.OpenPatientById(patientID);
                Course course = patient.Courses.FirstOrDefault(c => c.Id == testData["CourseID"]);
                PlanSetup planSetupLower = course.PlanSetups.FirstOrDefault(ps => ps.Id == testData["PlanIDLower"]);
                PlanSetup planSetupUpper = course.PlanSetups.FirstOrDefault(ps => ps.Id == testData["PlanIDUpper"]);

                SetUpContext(patient, course, planSetupLower, out PluginScriptContext scriptContextLower, out EsapiWorker esapiWorkerLower);

                patient.BeginModifications();
                try
                {
                    TestBuilder.Create()
                            .Add<ModelBaseTests>(new ModelBase(esapiWorkerLower), scriptContextLower)
                            .Add<ObjectiveSetupTests>(scriptContextLower.PlanSetup.OptimizationSetup, scriptContextLower.PlanSetup)
                            .Add<CalculationTests>(scriptContextLower.PlanSetup, scriptContextLower)
                            .Add<IsocenterTests>(scriptContextLower.PlanSetup, planSetupUpper, scriptContextLower)
                            .Add<StructureHelperTests>(scriptContextLower.StructureSet);
                    TestBase.RunTests();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Test execution failed.\n{e}");
                }
                finally
                {
                    EclipseApp.ClosePatient();
                }
            }
        }

        private static void SetUpContext(Patient patient, Course course, PlanSetup planSetupLower, out PluginScriptContext scriptContextLower, out EsapiWorker esapiWorkerLower)
        {
            scriptContextLower = new PluginScriptContext
            {
                Patient = patient,
                Course = course,
                PlanSetup = planSetupLower,
                StructureSet = planSetupLower.StructureSet
            };
            esapiWorkerLower = new EsapiWorker(scriptContextLower);
        }

        private static Dictionary<string, string> InitializeTestData()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            /* Read from txt file sensitive data
            * This file is added to .gitignore to exclude it from commits
            **/
            foreach (string line in File.ReadLines(Path.Combine("Configuration", "SensitiveData.txt")))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                string[] settingValue = line.Split(new char[] { '\t' });
                if (settingValue.Length == 1) continue;
                foreach (string val in settingValue.Skip(1))
                {
                    dict.Add(settingValue[0], val);
                }
            }
            return dict;
        }
    }
}