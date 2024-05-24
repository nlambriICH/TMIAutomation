using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using TMIAutomation.Language;
using TMIAutomation.View;

namespace TMIAutomation.ViewModel
{
    public class ScheduleViewModel : ViewModelBase
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
                    RetrieveLowerPlans(selectedCourseId);
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
                    CheckIsocentersOnArms(selectedCourseId, selectedUpperPlanId);
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

        private List<string> scheduleCourses;
        public List<string> ScheduleCourses
        {
            get => scheduleCourses;
            set
            {
                Set(ref scheduleCourses, value);
                SelectedScheduleCourseId = this.scheduleCourses.Count != 0 ? this.scheduleCourses[0] : string.Empty;
            }
        }

        private string selectedScheduleCourseId;
        public string SelectedScheduleCourseId
        {
            get => selectedScheduleCourseId;
            set
            {
                if (selectedScheduleCourseId != value)
                {
                    Set(ref selectedScheduleCourseId, value);
                }
            }
        }

        private DateTime selectedDate;
        public DateTime SelectedDate
        {
            get => selectedDate;
            set
            {
                if (selectedDate != value)
                {
                    Set(ref selectedDate, value);
                }
            }
        }

        private bool isIsoOnArmsChecked;
        public bool IsIsoOnArmsChecked
        {
            get => isIsoOnArmsChecked;
            set => Set(ref isIsoOnArmsChecked, value);
        }

        private readonly ModelBase modelBase;

        public ICommand StartExecutionCommand { get; }

        private double progress;
        public double Progress
        {
            get => progress;
            set => Set(ref progress, value);
        }

        public ScheduleViewModel(ModelBase modelBase)
        {
            this.modelBase = modelBase;
            StartExecutionCommand = new RelayCommand(StartExecution);
            SelectedDate = DateTime.Today;
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
            ScheduleCourses = await this.modelBase.GetCoursesAsync(schedule: true);
        }

        private async void RetrieveUpperPlans(string courseId)
        {
            UpperPlans = await this.modelBase.GetPlansAsync(courseId, ModelBase.PlanType.Up);
        }

        private async void RetrieveLowerPlans(string courseId)
        {
            LowerPlans = await this.modelBase.GetPlansAsync(courseId, ModelBase.PlanType.Down);
        }

        private async void CheckIsocentersOnArms(string courseId, string upperPlanId)
        {
            IsIsoOnArmsChecked = await this.modelBase.CheckIsocentersOnArmsAsync(courseId, upperPlanId);
        }

        private async void StartExecution()
        {
            ProgressBarViewModel pbViewModel = new ProgressBarViewModel("Scheduling");
            IProgress<double> progress = new Progress<double>(pbViewModel.IncrementProgress);
            IProgress<string> message = new Progress<string>(pbViewModel.UpdateMessage);
            ProgressBarWindow pbWindow = new ProgressBarWindow(pbViewModel);
            pbWindow.Show();

            bool success = true; // show "Complete" message box

            try
            {
                if (this.selectedScheduleCourseId == Resources.NewCourseListBox)
                {
                    List<string> ssStudySeriesId = await this.modelBase.GetSSStudySeriesIdAsync();
                    StructureSetSelectionViewModel ssSelectionViewModel = new StructureSetSelectionViewModel(ssStudySeriesId);
                    StructureSetSelection ssSelectionWindow = new StructureSetSelection(ssSelectionViewModel);
                    ssSelectionWindow.ShowDialog();

                    if (ssSelectionWindow.userClosing)
                    {
                        throw new InvalidOperationException(); // window closed by user
                    }

                    List<string> scheduleSSStudySeriesId = ssSelectionViewModel.ItemSelection.Where(s => s.IsChecked)
                            .Select(s => s.ItemName)
                            .ToList();

                    await this.modelBase.CreateScheduleCourseAsync();
                    ScheduleCourses = await this.modelBase.GetCoursesAsync();

                    await this.modelBase.SchedulePlansAsync(this.selectedCourseId,
                                                            this.selectedUpperPlanId,
                                                            this.selectedLowerPlanId,
                                                            this.selectedScheduleCourseId,
                                                            this.isIsoOnArmsChecked,
                                                            scheduleSSStudySeriesId,
                                                            progress,
                                                            message);
                }

                await this.modelBase.ComputeDisplacementsAsync(this.selectedCourseId,
                                                               this.selectedUpperPlanId,
                                                               this.selectedLowerPlanId,
                                                               this.selectedScheduleCourseId,
                                                               this.selectedDate,
                                                               this.isIsoOnArmsChecked,
                                                               progress,
                                                               message);
            }
            catch (InvalidOperationException)
            {
                success = false;
            }
            catch (Exception e)
            {
                success = false;
                throw new Exception("An error occurred during the lower-extremities workflow.", e);
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
