using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
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
                    RetrieveUpperPlans(selectedCourseId);
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

        public UpperViewModel(ModelBase modelBase)
        {
            this.modelBase = modelBase;
            IsJunctionChecked = true;
            IsControlChecked = true;
            IsOptimizationChecked = true;
            StartExecutionCommand = new RelayCommand(StartExecution);
            RetrieveCourses();
        }

        /*
         * Async void methods used only to set properties:
         * they cannot return a Task and need to be async to run on the ESAPI thread
         * Warning: there is no guarantee that these methods will be awaited
        */
        private async void RetrieveCourses()
        {
            Courses = await this.modelBase.GetCoursesAsync();
        }

        private async void RetrieveUpperPlans(string courseId)
        {
            UpperPlans = await this.modelBase.GetPlansAsync(courseId, ModelBase.PlanType.Up);
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
                List<string> oarIds = new List<string> { };
                if (this.isOptimizationChecked)
                {
                    List<string> structureNames = await this.modelBase.GetStructureNamesAsync(this.selectedCourseId, this.selectedPlanId);
                    OARSelectionViewModel oarSelectionViewModel = new OARSelectionViewModel(structureNames);
                    OARSelection oarSelectionWindow = new OARSelection(oarSelectionViewModel);
                    oarSelectionWindow.ShowDialog();

                    oarIds = oarSelectionViewModel.StructureSelection.Where(s => s.IsChecked)
                        .Select(s => s.StructureName)
                        .ToList();
                }

                if (this.isJunctionChecked)
                {
                    await this.modelBase.GenerateUpperJunctionAsync(this.selectedCourseId, this.selectedPlanId, this.selectedPTVId, progress, message);
                    UpperPTVs = await this.modelBase.GetPTVsFromPlanAsync(this.selectedCourseId, this.selectedPlanId);
                }

                if (this.isControlChecked)
                {
                    await this.modelBase.GenerateUpperControlAsync(this.selectedCourseId, this.selectedPlanId, this.selectedPTVId, progress, message);
                }

                if (this.isOptimizationChecked)
                {
                    await this.modelBase.OptimizeAsync(this.selectedCourseId, this.selectedPlanId, this.selectedPTVId, oarIds, progress, message);
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