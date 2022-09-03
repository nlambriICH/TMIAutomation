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

        public MainViewModel(EsapiWorker esapiWorker)
        {
            ModelBase modelBase = new ModelBase(esapiWorker);
            upperVM = new UpperViewModel(modelBase);
            lowerVM = new LowerViewModel(modelBase);
        }
    }
}
