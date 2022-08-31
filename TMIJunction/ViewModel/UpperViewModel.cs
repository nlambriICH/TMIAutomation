using GalaSoft.MvvmLight;
using System.Collections.Generic;
using TMIJunction.StructureCreation;

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

        private readonly BaseModel baseModel;

        public UpperViewModel(BaseModel baseModel)
        {
            this.baseModel = baseModel;
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
        }
    }
}