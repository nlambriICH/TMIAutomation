using GalaSoft.MvvmLight;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMIJunction.StructureCreation;

namespace TMIJunction.ViewModel
{
    public class LowerViewModel : ViewModelBase
    {
        private readonly LegsJunction legsJunction;

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

        private List<string> lowerPTV;
        public List<string> LowerPTV
        {
            get { return lowerPTV; }
            set { Set(ref lowerPTV, value); }
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

        public LowerViewModel(ModelBase modelBase)
        {
            this.modelBase = modelBase;
            IsJunctionChecked = true;
            IsControlChecked = true;
            IsOptimizationChecked = true;
            RetrieveData();
        }

        private async void RetrieveData()
        {
            Task<List<string>> upperPlansTask = this.modelBase.GetPlansAsync(ModelBase.PlanType.Up);
            Task<List<string>> registrationsTask = this.modelBase.GetRegistrationsAsync();
            Task<List<string>> lowerPlansTask = this.modelBase.GetPlansAsync(ModelBase.PlanType.Down);

            UpperPlans = await upperPlansTask;
            Registrations = await registrationsTask;
            LowerPlans = await lowerPlansTask;
        }
    }
}
