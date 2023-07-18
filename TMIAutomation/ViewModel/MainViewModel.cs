using System;
using GalaSoft.MvvmLight;
using TMIAutomation.Async;

namespace TMIAutomation.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public UpperViewModel UpperVM { get; }
        public LowerViewModel LowerVM { get; }

        public MainViewModel(EsapiWorker esapiWorker, IServiceProvider serviceProvider)
        {
            ModelBase modelBase = new ModelBase(esapiWorker, serviceProvider);
            UpperVM = new UpperViewModel(modelBase);
            LowerVM = new LowerViewModel(modelBase);
        }
    }
}