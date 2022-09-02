using GalaSoft.MvvmLight;
using TMIJunction.Async;
using TMIJunction.StructureCreation;

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
