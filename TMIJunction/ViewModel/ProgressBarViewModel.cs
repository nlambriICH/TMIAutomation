using GalaSoft.MvvmLight;
using Serilog;
using System;

namespace TMIJunction.ViewModel
{
	public sealed class ProgressBarViewModel : ViewModelBase
	{
		private double progress;
		public double Progress
		{
			get { return progress; }
			set { Set(ref progress, value); }
		}

		private string message;
		public string Message
		{
			get { return message; }
			set { Set(ref message, value); }
		}

		private string windowTitle;
		public string WindowTitle
		{
			get { return windowTitle; }
			set { Set(ref windowTitle, value); }
		}

		public int NumOperations { get; set; }

		public ProgressBarViewModel(string windowTitle, int numOperations = 1)
		{
			WindowTitle = windowTitle;
			Message = "Starting execution...";
			NumOperations = numOperations;
		}

		public void IncrementProgress(double p)
		{
			Progress += p / NumOperations;
		}

		public void ResetProgress()
		{
			Progress = 0;
		}

		public void UpdateMessage(string m)
		{
			Message = m;
		}

		public void ProgressBar_Closed(object sender, EventArgs e)
		{
			Log.CloseAndFlush();
		}
	}
}
