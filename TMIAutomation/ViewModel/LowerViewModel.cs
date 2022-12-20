using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using TMIAutomation.View;
using System.Linq;
using VMS.TPS.Common.Model.Types;
#if ESAPI15
using System.Windows;
#endif

namespace TMIAutomation.ViewModel
{
    public class LowerViewModel : ViewModelBase
    {
        private List<string> upperPlans;
        public List<string> UpperPlans
        {
            get => upperPlans;
            set
            {
                Set(ref upperPlans, value);
                SelectedUpperPlanId = this.upperPlans.Count != 0 ? this.upperPlans[0] : string.Empty;
            }
        }

        private string selectedUpperPlanId;
        public string SelectedUpperPlanId
        {
            get => selectedUpperPlanId;
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
            get => lowerPTVs;
            set
            {
                Set(ref lowerPTVs, value);
                SelectedLowerPTVId = this.LowerPTVs.Count != 0 ? this.lowerPTVs[0] : string.Empty;
            }
        }

        private string selectedLowerPTVId;
        public string SelectedLowerPTVId
        {
            get => selectedLowerPTVId;
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
            get => registrations;
            set
            {
                Set(ref registrations, value);
                SelectedRegistrationId = this.registrations.Count != 0 ? this.registrations[0] : string.Empty;
            }
        }

        private string selectedRegistrationId;
        public string SelectedRegistrationId
        {
            get => selectedUpperPlanId;
            set
            {
                if (selectedRegistrationId != value)
                {
                    Set(ref selectedRegistrationId, value);
                }
            }
        }

        private List<string> lowerPlans;
        public List<string> LowerPlans
        {
            get => lowerPlans;
            set
            {
                Set(ref lowerPlans, value);
                SelectedLowerPlanId = this.lowerPlans.Count != 0 ? this.lowerPlans[0] : string.Empty;
            }
        }

        private string selectedLowerPlanId;
        public string SelectedLowerPlanId
        {
            get => selectedLowerPlanId;
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
            get => isJunctionChecked;
            set => Set(ref isJunctionChecked, value);
        }

        private bool isControlChecked;
        public bool IsControlChecked
        {
            get => isControlChecked;
            set => Set(ref isControlChecked, value);
        }

        private bool isOptimizationChecked;
        public bool IsOptimizationChecked
        {
            get => isOptimizationChecked;
            set => Set(ref isOptimizationChecked, value);
        }

        private readonly ModelBase modelBase;
        public ICommand StartExecutionCommand { get; }

        private double progress;
        public double Progress
        {
            get => progress;
            set => Set(ref progress, value);
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
            LowerPlans = await lowerPlansTask;
            LowerPTVs = string.IsNullOrEmpty(this.selectedLowerPlanId)
                ? await this.modelBase.GetPTVsFromImgOrientationAsync(PatientOrientation.FeetFirstSupine)
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

            try
            {
#if ESAPI15
                bool generateBaseDosePlanOnly = false;
                if (this.isOptimizationChecked)
                {
                    MessageBoxResult response = MessageBox.Show("Yes: compute base-dose plan only\n"
                                                                + "No: perform automatic optimization using junction substructures",
                                                                "Lower-extremities optimization",
                                                                MessageBoxButton.YesNo,
                                                                MessageBoxImage.Question);
                    generateBaseDosePlanOnly = response == MessageBoxResult.Yes;
                }

                if (!generateBaseDosePlanOnly)
                {
                    await this.modelBase.GenerateLowerPlanAsync();
                }
#elif ESAPI16
                await this.modelBase.GenerateLowerPlanAsync();
#endif
                LowerPlans = await this.modelBase.GetPlansAsync(ModelBase.PlanType.Down);

                if (this.isJunctionChecked)
                {
                    bool isUpperPlanDoseValid = await this.modelBase.IsPlanDoseValidAsync(this.selectedUpperPlanId);
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
#if ESAPI16
                    await this.modelBase.OptimizeAsync(this.selectedUpperPlanId,
                                                       this.selectedRegistrationId,
                                                       this.selectedLowerPlanId,
                                                       progress,
                                                       message);
#else
                    await this.modelBase.OptimizeAsync(this.selectedUpperPlanId,
                                                       this.selectedRegistrationId,
                                                       this.selectedLowerPlanId,
                                                       generateBaseDosePlanOnly,
                                                       progress,
                                                       message);
#endif
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
