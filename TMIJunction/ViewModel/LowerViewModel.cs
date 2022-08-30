using GalaSoft.MvvmLight;
using System.Collections.Generic;

namespace TMIJunction.ViewModel
{
    public class LowerViewModel : ViewModelBase
    {
        private readonly LegsJunction legsJunction;

        private List<string> lowerPlans;
        public List<string> LowerPlans
        {
            get { return lowerPlans; }
            set { Set(ref lowerPlans, value); }
        }

        private List<string> lowerPTV;
        public List<string> LowerPTV {
            get { return lowerPTV; }
            set { Set(ref lowerPTV, value); }
        }

        private List<string> upperPlans;
        public List<string> UpperPlans
        {
            get { return upperPlans; }
            set { Set(ref upperPlans, value); }
        }

        public LowerViewModel(LegsJunction legsJunction)
        {
            this.legsJunction = legsJunction;
            UpperPlans = this.legsJunction.GetUpperPlans().Result;
            LowerPlans = this.legsJunction.GetLowerPlans().Result;
        }

    }
}
