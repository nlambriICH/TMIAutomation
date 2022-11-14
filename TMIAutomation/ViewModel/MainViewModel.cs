using GalaSoft.MvvmLight;
using Serilog;
using System;
using TMIAutomation.Async;
using TMIAutomation.StructureCreation;

namespace TMIAutomation.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public UpperViewModel UpperVM { get; }
        public LowerViewModel LowerVM { get; }

        public MainViewModel(EsapiWorker esapiWorker)
        {
            ModelBase modelBase = new ModelBase(esapiWorker);
            UpperVM = new UpperViewModel(modelBase);
            LowerVM = new LowerViewModel(modelBase);
        }
    }
}
