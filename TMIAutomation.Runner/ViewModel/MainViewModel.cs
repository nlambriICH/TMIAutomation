using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;

namespace TMIAutomation.Runner
{
    internal class MainViewModel : ViewModelBase
    {
        private readonly ScriptRunner pluginRunner;

        public MainViewModel(ScriptRunner pluginRunner)
        {
            this.pluginRunner = pluginRunner;
            Recents = pluginRunner.GetRecentEntries().Reverse().ToList();
        }

        private string searchText;
        public string SearchText
        {
            get => searchText;
            set => Set(ref searchText, value);
        }

        private IEnumerable<PatientMatch> patientMatches;
        public IEnumerable<PatientMatch> PatientMatches
        {
            get => patientMatches;
            set => Set(ref patientMatches, value);
        }

        private PatientMatch selectedPatientMatch;
        public PatientMatch SelectedPatientMatch
        {
            get => selectedPatientMatch;
            set => Set(ref selectedPatientMatch, value);
        }

        // Must not be IEnumerable or it re-creates the view models
        // when accessed in OpenPatient(), losing which items are active
        private IList<PlanOrPlanSumViewModel> plansAndPlanSums;
        public IList<PlanOrPlanSumViewModel> PlansAndPlanSums
        {
            get => plansAndPlanSums;
            set => Set(ref plansAndPlanSums, value);
        }

        private IList<RecentEntry> recents;
        public IList<RecentEntry> Recents
        {
            get => recents;
            set => Set(ref recents, value);
        }

        private RecentEntry selectedRecent;
        public RecentEntry SelectedRecent
        {
            get => selectedRecent;
            set => Set(ref selectedRecent, value);
        }

        public ICommand SearchPatientCommand => new RelayCommand(SearchPatient);
        public ICommand OpenPatientCommand => new RelayCommand(OpenPatient);
        public ICommand OpenRecentEntryCommand => new RelayCommand(OpenRecentEntry);
        public ICommand RunCommand => new RelayCommand(Run);

        private void SearchPatient()
        {
            PatientMatches = pluginRunner.FindPatientMatches(SearchText);

            SelectedPatientMatch = null;
            PlansAndPlanSums = null;
        }

        private void OpenPatient()
        {
            PlanOrPlanSum[] plansAndPlanSums = pluginRunner.GetPlansAndPlanSumsOfPatient(SelectedPatientMatch?.Id);
            PlansAndPlanSums = plansAndPlanSums?.Select(CreatePlanOrPlanSumViewModel).ToList();
        }

        private PlanOrPlanSumViewModel CreatePlanOrPlanSumViewModel(PlanOrPlanSum planOrPlanSum)
        {
            return new PlanOrPlanSumViewModel
            {
                PlanType = planOrPlanSum.PlanType,
                Id = planOrPlanSum.Id,
                CourseId = planOrPlanSum.CourseId
            };
        }

        private void OpenRecentEntry()
        {
            SearchText = SelectedRecent?.PatientId;
            SearchPatient();
            SelectedPatientMatch = PatientMatches?.FirstOrDefault();
            OpenPatient();
            CheckInScopeOrActiveFromRecent();
        }

        private void CheckInScopeOrActiveFromRecent()
        {
            if (PlansAndPlanSums == null) return;

            foreach (PlanOrPlanSumViewModel planOrPlanSumViewModel in PlansAndPlanSums)
            {
                if (IsRecentInScope(planOrPlanSumViewModel))
                {
                    planOrPlanSumViewModel.IsInScope = true;
                }
                if (IsRecentActive(planOrPlanSumViewModel))
                {
                    planOrPlanSumViewModel.IsActive = true;
                }
            }
        }

        private bool IsRecentInScope(PlanOrPlanSumViewModel planOrPlanSumVm)
        {
            return SelectedRecent?.PlansAndPlanSumsInScope?.Any(p => p.CourseId == planOrPlanSumVm?.CourseId && p.Id == planOrPlanSumVm?.Id) ?? false;
        }

        private bool IsRecentActive(PlanOrPlanSumViewModel planOrPlanSumVm)
        {
            return SelectedRecent?.ActivePlan?.CourseId == planOrPlanSumVm?.CourseId && SelectedRecent?.ActivePlan?.Id == planOrPlanSumVm?.Id;
        }

        private void Run()
        {
            pluginRunner.RunScript(SelectedPatientMatch?.Id, GetPlansAndPlanSumsInScope(), GetActivePlan());

            IList<RecentEntry> recents = Recents;
            RecentEntry selectedRecent = SelectedRecent;

            Recents = pluginRunner.GetRecentEntries().Reverse().ToList();

            // If recents changed, it means a new recent was added, so select it as the recent;
            // otherwise, select the previously selected recent
            SelectedRecent = recents.Count != Recents.Count
                ? Recents.FirstOrDefault()
                : selectedRecent;
            OpenRecentEntry();
        }

        private PlanOrPlanSum[] GetPlansAndPlanSumsInScope()
        {
            return PlansAndPlanSums?.Where(p => p.IsInScope).Select(CreatePlanOrPlanSum).ToArray();
        }

        private PlanOrPlanSum GetActivePlan()
        {
            return PlansAndPlanSums?.Where(p => p.IsActive).Select(CreatePlanOrPlanSum).FirstOrDefault();
        }

        private PlanOrPlanSum CreatePlanOrPlanSum(PlanOrPlanSumViewModel planOrPlanSumViewModel)
        {
            return new PlanOrPlanSum
            {
                PlanType = planOrPlanSumViewModel.PlanType,
                Id = planOrPlanSumViewModel.Id,
                CourseId = planOrPlanSumViewModel.CourseId
            };
        }
    }
}