﻿using System;
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
                PlanSetup planSetup = course.PlanSetups.FirstOrDefault(ps => ps.Id == testData["PlanID"]);
                PluginScriptContext scriptContext = new PluginScriptContext
                {
                    Patient = patient,
                    Course = course,
                    PlanSetup = planSetup,
                    StructureSet = planSetup.StructureSet
                };
                EsapiWorker esapiWorker = new EsapiWorker(scriptContext);

                patient.BeginModifications();
                try
                {
                    TestBuilder.Create()
                            .Add<ModelBaseTests>(new ModelBase(esapiWorker), scriptContext)
                            .Add<ObjectiveSetupTests>(scriptContext.PlanSetup.OptimizationSetup, scriptContext.PlanSetup)
                            .Add<CalculationTests>(scriptContext.PlanSetup, scriptContext)
                            .Add<IsocenterTests>(scriptContext.PlanSetup, scriptContext)
                            .Add<StructureHelperTests>(scriptContext.StructureSet);
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