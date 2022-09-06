using GalaSoft.MvvmLight;
using Serilog;
using System;
using TMIJunction.Async;
using TMIJunction.StructureCreation;

namespace TMIJunction.ViewModel
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

		public void MainView_Closed(object sender, EventArgs e)
		{
			Log.CloseAndFlush();
		}
	}
}
