using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using TMIJunction.StructureCreation;
using TMIJunction.View;

namespace TMIJunction.ViewModel
{
    public class UpperViewModel : ViewModelBase
    {
        private List<string> upperPlans;
        public List<string> UpperPlans
        {
            get { return upperPlans; }
            set
            {
                Set(ref upperPlans, value);
                SelectedPlanId = this.upperPlans.Count != 0 ? this.upperPlans[0] : string.Empty;
            }
        }

        private string selectedPlanId;
        public string SelectedPlanId
        {
            get { return selectedPlanId; }
            set
            {
                if (selectedPlanId != value)
                {
                    Set(ref selectedPlanId, value);
                    RetrieveUpperPTVs(selectedPlanId);
                }
            }
        }

        private List<string> upperPTVs;
        public List<string> UpperPTVs
        {
            get { return upperPTVs; }
            set
            {
                Set(ref upperPTVs, value);
                SelectedPTVId = this.upperPTVs.Count != 0 ? this.upperPTVs[0] : string.Empty;
            }
        }

        private string selectedPTVId;
        public string SelectedPTVId
        {
            get { return selectedPTVId; }
            set { Set(ref selectedPTVId, value); }
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

        private readonly ModelBase modelBase;

        public ICommand StartOrCancelExecutionCommand { get; }

        private double progress;
        public double Progress
        {
            get { return progress; }
            set { Set(ref progress, value); }
        }

        public UpperViewModel(ModelBase modelBase)
        {
            this.modelBase = modelBase;
            IsJunctionChecked = true;
            IsControlChecked = true;
            StartOrCancelExecutionCommand = new RelayCommand(StartExecution);
            RetrieveUpperPlans();
        }

        private async void RetrieveUpperPlans()
        {
            UpperPlans = await this.modelBase.GetPlansAsync(ModelBase.PlanType.Up);
        }

        private async void RetrieveUpperPTVs(string planId)
        {
            UpperPTVs = await this.modelBase.GetPTVsOfPlanAsync(planId);
        }

        private async void StartExecution()
        {
            ProgressBarViewModel pbViewModel = new ProgressBarViewModel("Upper-body");
            Progress<double> progress = new Progress<double>(pbViewModel.IncrementProgress);
            Progress<string> message = new Progress<string>(pbViewModel.UpdateMessage);
            ProgressBarWindow pbWindow = new ProgressBarWindow(pbViewModel);
            pbWindow.Show();

            if (this.isJunctionChecked && this.isControlChecked)
            {
                pbViewModel.NumOperations++; // rescale the progress bar update
            }

            if (this.isJunctionChecked)
            {
                await this.modelBase.GenerateUpperJunctionAsync(this.selectedPlanId, this.selectedPTVId, progress, message);
                RetrieveUpperPTVs(this.selectedPlanId);
            }

            if (this.isControlChecked)
            {
                await this.modelBase.GenerateUpperControlAsync(this.selectedPlanId, this.selectedPTVId, progress, message);
            }

            pbViewModel.ResetProgress();
            pbWindow.Close();
        }
    }
}