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
            set { Set(ref upperPlans, value); }
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
            set { Set(ref upperPTVs, value); }
        }

        private string selectedPTVId;
        public string SelectedPTVId
        {
            get { return selectedPTVId; }
            set { Set(ref selectedPTVId, value); }
        }

        private bool isUpperJunctionChecked;
        public bool IsUpperJunctionChecked
        {
            get { return isUpperJunctionChecked; }
            set { Set(ref isUpperJunctionChecked, value); }
        }

        private bool isUpperControlChecked;
        public bool IsUpperControlChecked
        {
            get { return isUpperControlChecked; }
            set { Set(ref isUpperControlChecked, value); }
        }

        private readonly BaseModel baseModel;

        public ICommand StartOrCancelExecutionCommand { get; }

        private double progress;
        public double Progress
        {
            get { return progress; }
            set { Set(ref progress, value); }
        }

        public UpperViewModel(BaseModel baseModel)
        {
            this.baseModel = baseModel;
            IsUpperJunctionChecked = true;
            IsUpperControlChecked = true;
            StartOrCancelExecutionCommand = new RelayCommand(StartOrCancelExecution);
            RetrieveUpperPlans();
        }

        private async void RetrieveUpperPlans()
        {
            UpperPlans = await this.baseModel.GetUpperPlansAsync();
            SelectedPlanId = this.upperPlans.Count != 0 ? this.upperPlans[0] : string.Empty;
        }

        private async void RetrieveUpperPTVs(string planId)
        {
            UpperPTVs = await this.baseModel.GetPTVsOfPlanAsync(planId);
            SelectedPTVId = this.upperPTVs.Count != 0 ? this.upperPTVs[0] : string.Empty;
        }

        private async void StartOrCancelExecution()
        {

            ProgressBarViewModel pbViewModel = new ProgressBarViewModel("Upper-body");
            Progress<double> progress = new Progress<double>(pbViewModel.IncrementProgress);
            Progress<string> message = new Progress<string>(pbViewModel.UpdateMessage);
            ProgressBarWindow pbWindow = new ProgressBarWindow(pbViewModel);
            pbWindow.Show();

            if (this.isUpperJunctionChecked && this.isUpperControlChecked)
            {
                pbViewModel.NumOperations++; // rescale the progress bar update
            }

            if (this.isUpperJunctionChecked)
            {
                await this.baseModel.GenerateUpperJunctionAsync(this.selectedPlanId, this.selectedPTVId, progress, message);
                RetrieveUpperPTVs(this.selectedPlanId);
            }

            if (this.isUpperControlChecked)
            {
                await this.baseModel.GenerateUpperControlAsync(this.selectedPlanId, this.selectedPTVId, progress, message);
            }

            pbViewModel.ResetProgress();
            pbWindow.Close();
        }
    }
}