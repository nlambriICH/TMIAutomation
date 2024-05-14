using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
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

        private async void RetrieveLowerPlans(string courseId)
        {
            LowerPlans = await this.modelBase.GetPlansAsync(courseId, ModelBase.PlanType.Down);
        }

        private async void StartExecution()
        {
            ProgressBarViewModel pbViewModel = new ProgressBarViewModel("Scheduling");
            IProgress<double> progress = new Progress<double>(pbViewModel.IncrementProgress);
            IProgress<string> message = new Progress<string>(pbViewModel.UpdateMessage);
            ProgressBarWindow pbWindow = new ProgressBarWindow(pbViewModel);
            pbWindow.Show();


        }
    }
}
