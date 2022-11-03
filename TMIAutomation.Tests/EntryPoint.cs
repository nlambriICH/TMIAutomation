using System;
using TMIAutomation.Async;
using TMIAutomation.StructureCreation;
using VMS.TPS.Common.Model.API;
using TMIAutomation.Tests.ReadOnlyTests;
using System.IO;
using System.Collections.Generic;

namespace TMIAutomation.Tests
{
    internal class EntryPoint : IDisposable
    {
        private static Application EclipseApp { get; set; }
        private static readonly Dictionary<string, string> testData = InitializeTestData();

        [STAThread]
        public static void Main(string[] args)
        {
            EclipseApp = Application.CreateApplication();
            string patientID = testData["PatientID"];
            Patient patient = EclipseApp.OpenPatientById(patientID);
            PluginScriptContext scriptContext = new PluginScriptContext
            {
                Patient = patient
            };
            EsapiWorker esapiWorker = new EsapiWorker(scriptContext);

            TestBuilder.Create()
                .Add<ModelBaseTests>(new ModelBase(esapiWorker), scriptContext)
                .RunTests();
        }

        private static Dictionary<string, string> InitializeTestData()
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            /* Read from txt file sensitive data
            * This file is added to .gitignore to exclude it from commits
            **/
            foreach (string line in File.ReadLines("SensitiveData.txt"))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                string[] settingValue = line.Split(new char[] { '\t' }, 2);
                if (settingValue.Length == 1) continue;
                dict.Add(settingValue[0], settingValue[1]);
            }
            return dict;
        }

        public void Dispose()
        {
            EclipseApp.ClosePatient();
            EclipseApp.Dispose();
        }
    }
}
