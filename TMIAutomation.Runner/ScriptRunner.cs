using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using TMIAutomation.Runner.Persistence;
using VMS.TPS;
using VMS.TPS.Common.Model.API;
using Application = VMS.TPS.Common.Model.API.Application;

namespace TMIAutomation.Runner
{
    internal class ScriptRunner : IDisposable
    {
        private const string DataFileName = "runner.yaml";
        private const string ApplicationName = "External Beam Planning";
        private const string VersionInfo = "15.6.6.52";
        private const int MaxSearchResults = 20;
        private readonly Script script;
        private Application esapiApp;
        private PatientSummarySearch patientSummarySearch;
        private IDataRepository dataRepository;

        public ScriptRunner(Script script)
        {
            this.script = script;
            Initialize();
        }

        private void Initialize()
        {
            this.esapiApp = Application.CreateApplication();
            this.patientSummarySearch = new PatientSummarySearch(esapiApp.PatientSummaries, MaxSearchResults);
            this.dataRepository = new DataRepository(GetDataPath());
        }

        public void Dispose()
        {
            esapiApp.Dispose();
        }

        private string GetDataPath()
        {
            return Path.Combine(GetAssemblyDirectory(), DataFileName);
        }

        private string GetAssemblyDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public PatientMatch[] FindPatientMatches(string searchText)
        {
            return patientSummarySearch.FindMatches(searchText).Select(CreatePatientMatch).ToArray();
        }

        public PlanOrPlanSum[] GetPlansAndPlanSumsOfPatient(string patientId)
        {
            if (patientId == null)
            {
                return null;
            }

            Patient patient = esapiApp.OpenPatientById(patientId);

            // Must call ToArray() before closing the patient
            PlanOrPlanSum[] plansAndPlanSums = patient?.GetPlanningItems().Select(CreatePlanOrPlanSum).ToArray();

            esapiApp.ClosePatient();

            return plansAndPlanSums;
        }

        public void RunScript(string patientId, IEnumerable<PlanOrPlanSum> plansAndPlanSumsInScope, PlanOrPlanSum activePlan)
        {
            try
            {
                SaveRecent(patientId, plansAndPlanSumsInScope, activePlan);

                Patient patient = esapiApp.OpenPatientById(patientId);
                var context = CreateScriptContext(patient, plansAndPlanSumsInScope, activePlan);

                if (script != null)
                {
                    script.Run(context);
                }
            }
            catch (Exception e)
            {
                // Mimic Eclipse by showing a message box when an exception is thrown
                MessageBox.Show(e.Message, ApplicationName, MessageBoxButton.OK, MessageBoxImage.Exclamation);
            }
            finally
            {
                // currently the ScriptRunner does not save after execution
                esapiApp.ClosePatient();
            }
        }

        public IList<RecentEntry> GetRecentEntries()
        {
            Data data = dataRepository.Load();
            return data?.Recents;
        }

        private PatientMatch CreatePatientMatch(PatientSummary patientSummary)
        {
            return new PatientMatch
            {
                Id = patientSummary.Id,
                LastName = patientSummary.LastName,
                FirstName = patientSummary.FirstName
            };
        }

        private PlanOrPlanSum CreatePlanOrPlanSum(PlanningItem plan)
        {
            return new PlanOrPlanSum
            {
                PlanType = GetPlanType(plan),
                Id = plan.Id,
                CourseId = plan.GetCourse().Id
            };
        }

        private PlanningItemType GetPlanType(PlanningItem plan)
        {
            switch (plan)
            {
                case PlanSetup _:
                    return PlanningItemType.Plan;
                case PlanSum _:
                    return PlanningItemType.PlanSum;
                default:
                    throw new InvalidOperationException("Unknown plan type.");
            }
        }

        private void SaveRecent(string patientId, IEnumerable<PlanOrPlanSum> plansAndPlanSumsInScope, PlanOrPlanSum activePlan)
        {
            Data data = dataRepository.Load();

            RecentEntry recent = new RecentEntry
            {
                PatientId = patientId,
                PlansAndPlanSumsInScope = plansAndPlanSumsInScope?.ToList(),
                ActivePlan = activePlan
            };

            if (!data.Recents.Contains(recent))
            {
                data.Recents.Add(recent);
                dataRepository.Save(data);
            }
        }

        private PluginScriptContext CreateScriptContext(Patient patient, IEnumerable<PlanOrPlanSum> plansAndPlanSumsInScope, PlanOrPlanSum activePlan)
        {
            PlanningItem[] planningItems = patient?.GetPlanningItems().ToArray();
            PlanningItem[] planningItemsInScope = FindPlanningItems(plansAndPlanSumsInScope, planningItems);
            var planSetup = FindPlanningItem(activePlan, planningItems) as PlanSetup;

            return new PluginScriptContext
            {
                CurrentUser = esapiApp.CurrentUser,
                Course = planSetup?.Course,
                Image = planSetup?.StructureSet?.Image,
                StructureSet = planSetup?.StructureSet,
                Patient = patient,
                PlanSetup = planSetup,
                ExternalPlanSetup = planSetup as ExternalPlanSetup,
                BrachyPlanSetup = planSetup as BrachyPlanSetup,
                PlansInScope = planningItemsInScope?.Where(p => p is PlanSetup).Cast<PlanSetup>(),
                ExternalPlansInScope = planningItemsInScope?.Where(p => p is ExternalPlanSetup).Cast<ExternalPlanSetup>(),
                BrachyPlansInScope = planningItemsInScope?.Where(p => p is BrachyPlanSetup).Cast<BrachyPlanSetup>(),
                PlanSumsInScope = planningItemsInScope?.Where(p => p is PlanSum).Cast<PlanSum>(),
                ApplicationName = ApplicationName,
                VersionInfo = VersionInfo
            };
        }

        private PlanningItem[] FindPlanningItems(IEnumerable<PlanOrPlanSum> plansAndPlanSums, IEnumerable<PlanningItem> planningItems)
        {
            return plansAndPlanSums?.Select(p => FindPlanningItem(p, planningItems)).ToArray();
        }

        private PlanningItem FindPlanningItem(PlanOrPlanSum planOrPlanSum, IEnumerable<PlanningItem> planningItems)
        {
            return planningItems?.FirstOrDefault(p => p.GetCourse().Id == planOrPlanSum?.CourseId && p.Id == planOrPlanSum?.Id);
        }
    }
}
