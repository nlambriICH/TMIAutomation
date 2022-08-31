using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMIJunction.Async;
using TMIJunction.StructureCreation;
using VMS.TPS.Common.Model.API;

namespace TMIJunction.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private UpperViewModel upperVM;
        public UpperViewModel UpperVM { get { return upperVM; } }

        private LowerViewModel lowerVM;
        public LowerViewModel LowerVM { get { return lowerVM; } }

        public MainViewModel(EsapiWorker esapiWorker, LegsJunction legsJunction)
        {
            var baseModel = new BaseModel(esapiWorker);
            upperVM = new UpperViewModel(baseModel);
            lowerVM = new LowerViewModel(new LegsJunction(esapiWorker));
        }
    }
}
