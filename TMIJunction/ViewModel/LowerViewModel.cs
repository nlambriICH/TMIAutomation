using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using TMIJunction.StructureCreation;
using TMIJunction.View;
using System.Linq;
using System.Windows.Forms;

namespace TMIJunction.ViewModel
{
    public class LowerViewModel : ViewModelBase
    {
        private List<string> upperPlans;
        public List<string> UpperPlans
        {
            get { return upperPlans; }
            set
            {
                Set(ref upperPlans, value);
                SelectedUpperPlanId = this.upperPlans.Count != 0 ? this.upperPlans[0] : string.Empty;
            }
        }

        private string selectedUpperPlanId;
        public string SelectedUpperPlanId
        {
            get { return selectedUpperPlanId; }
            set
            {
                if (selectedUpperPlanId != value)
                {
                    Set(ref selectedUpperPlanId, value);
                }
            }
        }

        private List<string> lowerPTVs;
        public List<string> LowerPTVs
        {
            get { return lowerPTVs; }
            set
            {
                Set(ref lowerPTVs, value);
                SelectedLowerPTVId = this.LowerPTVs.Count != 0 ? this.lowerPTVs[0] : string.Empty;
            }
        }

        private string selectedLowerPTVId;
        public string SelectedLowerPTVId
        {
            get { return selectedLowerPTVId; }
            set
            {
                if (selectedLowerPTVId != value)
                {
                    Set(ref selectedLowerPTVId, value);
                }
            }
        }

        private List<string> registrations;
        public List<string> Registrations

        {
            get { return registrations; }
            set
            {
                Set(ref registrations, value);
                SelectedRegistrationId = this.registrations.Count != 0 ? this.registrations[0] : string.Empty;
            }
        }

        private string selectedRegistrationId;
        public string SelectedRegistrationId
        {
            get { return selectedUpperPlanId; }
            set
            {
                if (selectedRegistrationId != value)
                {
                    Set(ref selectedRegistrationId, value);
                }
            }
        }

        private string machineName;
        public string MachineName
        {
            get { return machineName; }
            set
            {
                if (machineName != value)
                {
                    Set(ref machineName, value);
                }
            }
        }

        private List<string> lowerPlans;
        public List<string> LowerPlans
        {
            get { return lowerPlans; }
            set
            {
                Set(ref lowerPlans, value);
                SelectedLowerPlanId = this.lowerPlans.Count != 0 ? this.lowerPlans[0] : string.Empty;
            }
        }

        private string selectedLowerPlanId;
        public string SelectedLowerPlanId
        {
            get { return selectedLowerPlanId; }
            set
            {
                if (selectedLowerPlanId != value)
                {
                    Set(ref selectedLowerPlanId, value);
                }
            }
        }

        private bool isJunctionChecked;
        public bool IsJunctionChecked
        {
            get { return isJunctionChecked; }
            set { Set(ref isJunctionChecked, value); }
        }

        private bool isControlChecked;
        public bool IsControlChecked
        {
            get { return isControlChecked; }
            set { Set(ref isControlChecked, value); }
        }

        private bool isOptimizationChecked;
        public bool IsOptimizationChecked
        {
            get { return isOptimizationChecked; }
            set { Set(ref isOptimizationChecked, value); }
        }

        private readonly ModelBase modelBase;
        public ICommand StartExecutionCommand { get; }

        private double progress;
        public double Progress
        {
            get { return progress; }
            set { Set(ref progress, value); }
        }

        public LowerViewModel(ModelBase modelBase)
        {
            this.modelBase = modelBase;
            IsJunctionChecked = true;
            IsControlChecked = true;
            IsOptimizationChecked = true;
            StartExecutionCommand = new RelayCommand(StartExecution);
            RetrieveData();
        }

        private async void RetrieveData()
        {
            Task<List<string>> upperPlansTask = this.modelBase.GetPlansAsync(ModelBase.PlanType.Up);
            Task<List<string>> registrationsTask = this.modelBase.GetRegistrationsAsync();
            Task<List<string>> lowerPlansTask = this.modelBase.GetPlansAsync(ModelBase.PlanType.Down);

            UpperPlans = await upperPlansTask;
            MachineName = await this.modelBase.GetMachineNameAsync(this.selectedUpperPlanId);
            LowerPlans = await lowerPlansTask;
            LowerPTVs = string.IsNullOrEmpty(this.selectedLowerPlanId)
                ? await this.modelBase.GetPTVsFromSSAsync(this.upperPlans)
                : await this.modelBase.GetPTVsFromPlanAsync(this.selectedLowerPlanId);

            Registrations = await registrationsTask;
        }

        private async void StartExecution()
        {
            ProgressBarViewModel pbViewModel = new ProgressBarViewModel("Lower-extremities");
            IProgress<double> progress = new Progress<double>(pbViewModel.IncrementProgress);
            IProgress<string> message = new Progress<string>(pbViewModel.UpdateMessage);
            ProgressBarWindow pbWindow = new ProgressBarWindow(pbViewModel);
            pbWindow.Show();

            bool[] checkedOptions = new bool[] { this.isJunctionChecked, this.IsControlChecked, this.isOptimizationChecked };
            int rescaleProgress = checkedOptions.Count(c => c); // count how many CheckBox are checked
            pbViewModel.NumOperations += rescaleProgress - 1; // rescale the progress bar update

            await this.modelBase.GenerateLowerPlanAsync(this.upperPlans);
            LowerPlans = await this.modelBase.GetPlansAsync(ModelBase.PlanType.Down);

            try
            {
                if (this.isJunctionChecked)
                {
                    bool isUpperPlanDoseValid = await this.modelBase.IsPlanDoseValid(this.selectedUpperPlanId);
                    if (!isUpperPlanDoseValid)
                    {
                        throw new InvalidOperationException($"The selected upper-body plan {this.selectedUpperPlanId} has invalid dose." +
                            $"The upper-body plan should have a calculated dose distribution assigned.");
                    }

                    await this.modelBase.GenerateLowerJunctionAsync(this.selectedUpperPlanId,
                                                                    this.selectedLowerPlanId,
                                                                    this.selectedLowerPTVId,
                                                                    this.selectedRegistrationId,
                                                                    progress,
                                                                    message);
                    LowerPTVs = await this.modelBase.GetPTVsFromPlanAsync(this.selectedLowerPlanId);
                }

                if (this.isControlChecked)
                {
                    await this.modelBase.GenerateLowerControlAsync(this.selectedLowerPlanId,
                                                                   this.isJunctionChecked ? StructureHelper.PTV_TOTAL : this.selectedLowerPTVId,
                                                                   progress, message);
                }

                if (this.isOptimizationChecked)
                {
                    await this.modelBase.OptimizeAsync(this.selectedLowerPlanId, this.machineName, progress, message);
                }
            }
            finally
            {
                pbViewModel.ResetProgress();
                pbWindow.Close();
            }
        }
    }
}
