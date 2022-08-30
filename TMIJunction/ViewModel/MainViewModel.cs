using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace TMIJunction.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private bool isCalculationInProgress;

        private UpperViewModel upperVM;
        public UpperViewModel UpperVM { get { return upperVM; } }

        private LowerViewModel lowerVM;
        public LowerViewModel LowerVM { get { return lowerVM; } }

        public MainViewModel(LegsJunction legsJunction)
        {
            upperVM = new UpperViewModel();
            lowerVM = new LowerViewModel(legsJunction);

            isCalculationInProgress = false;
        }

        private List<string> plans;
        public List<string> Plans
        {
            get { return plans; }
            set { Set(ref plans, value); }
        }

    }
}
