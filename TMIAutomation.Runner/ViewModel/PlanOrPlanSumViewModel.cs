using GalaSoft.MvvmLight;

namespace TMIAutomation.Runner
{
    internal class PlanOrPlanSumViewModel : ViewModelBase
    {
        private PlanningItemType planType;
        public PlanningItemType PlanType
        {
            get => planType;
            set
            {
                if (Set(ref planType, value))
                {
                    RaisePropertyChanged(nameof(CanBeActive));
                }
            }
        }

        private string id;
        public string Id
        {
            get => id;
            set => Set(ref id, value);
        }

        private string courseId;
        public string CourseId
        {
            get => courseId;
            set => Set(ref courseId, value);
        }

        private bool isInScope;
        public bool IsInScope
        {
            get => isInScope;
            set
            {
                if (Set(ref isInScope, value))
                {
                    if (!IsInScope)
                    {
                        IsActive = false;
                    }
                    RaisePropertyChanged(nameof(CanBeActive));
                }
            }
        }

        private bool isActive;
        public bool IsActive
        {
            get => isActive;
            set => Set(ref isActive, value);
        }

        public bool CanBeActive => PlanType == PlanningItemType.Plan && IsInScope;
    }
}