using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using TMIAutomation.View;

namespace TMIAutomation.ViewModel
{
    public class UpperViewModel : ViewModelBase
    {
        private List<string> courses;
        public List<string> Courses
        {
            get => courses;
            set
            {
                Set(ref courses, value);
                SelectedCourseId = this.courses.Count != 0 ? this.courses[0] : string.Empty;
            }
        }

        private string selectedCourseId;
        public string SelectedCourseId
        {
            get => selectedCourseId;
            set
            {
                if (selectedCourseId != value)
                {
                    Set(ref selectedCourseId, value);
                    this.RetrieveUpperPlans();
                }
            }
        }

        private List<string> upperPlans;
        public List<string> UpperPlans
        {
            get => upperPlans;
            set
            {
                Set(ref upperPlans, value);
                SelectedPlanId = this.upperPlans.Count != 0 ? this.upperPlans[0] : string.Empty;
            }
        }

        private string selectedPlanId;
        public string SelectedPlanId
        {
            get => selectedPlanId;
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
            get => upperPTVs;
            set
            {
                Set(ref upperPTVs, value);
                SelectedPTVId = this.upperPTVs.Count != 0 ? this.upperPTVs[0] : string.Empty;
            }
        }

        private string selectedPTVId;
        public string SelectedPTVId
        {
            get => selectedPTVId;
            set => Set(ref selectedPTVId, value);
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

        private readonly ModelBase modelBase;

        public ICommand StartExecutionCommand { get; }

        private double progress;
        public double Progress
        {
            get => progress;
            set => Set(ref progress, value);
        }

        public UpperViewModel(ModelBase modelBase)
        {
            this.modelBase = modelBase;
            IsJunctionChecked = true;
            IsControlChecked = true;
            StartExecutionCommand = new RelayCommand(StartExecution);
            RetrieveCourses();
        }

        private async void RetrieveCourses()
        {
            Courses = await this.modelBase.GetCoursesAsync();
        }

        private async void RetrieveUpperPlans()
        {
            UpperPlans = await this.modelBase.GetPlansAsync(this.selectedCourseId, ModelBase.PlanType.Up);
        }

        private async void RetrieveUpperPTVs(string planId)
        {
            UpperPTVs = await this.modelBase.GetPTVsFromPlanAsync(this.selectedCourseId, planId);
        }

        private async void StartExecution()
        {
            ProgressBarViewModel pbViewModel = new ProgressBarViewModel("Upper-body");
            IProgress<double> progress = new Progress<double>(pbViewModel.IncrementProgress);
            IProgress<string> message = new Progress<string>(pbViewModel.UpdateMessage);
            ProgressBarWindow pbWindow = new ProgressBarWindow(pbViewModel);
            pbWindow.Show();

            bool[] checkedOptions = new bool[] { this.isJunctionChecked, this.IsControlChecked };
            int rescaleProgress = checkedOptions.Count(c => c); // count how many CheckBox are checked
            pbViewModel.NumOperations += rescaleProgress - 1; // rescale the progress bar update
            bool success = true; // show "Complete" message box

            try
            {
                if (this.isJunctionChecked)
                {
                    await this.modelBase.GenerateUpperJunctionAsync(this.selectedPlanId, this.selectedPTVId, progress, message);
                    RetrieveUpperPTVs(this.selectedPlanId);
                }

                if (this.isControlChecked)
                {
                    await this.modelBase.GenerateUpperControlAsync(this.selectedPlanId, this.selectedPTVId, progress, message);
                }
            }
            catch (Exception e)
            {
                success = false;
                throw new Exception("An error occurred during the upper-extremities workflow.", e);
            }
            finally
            {
                pbViewModel.ResetProgress();
                pbWindow.Close();
                if (success)
                {
                    MessageBox.Show("Completed!",
                    "Lower-extremities optimization",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                }
            }
        }
    }
}