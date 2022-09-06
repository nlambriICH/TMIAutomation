using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using TMIAutomation.StructureCreation;
using TMIAutomation.View;

namespace TMIAutomation.ViewModel
{
	public class UpperViewModel : ViewModelBase
	{
		private List<string> upperPlans;
		public List<string> UpperPlans
		{
			get { return upperPlans; }
			set
			{
				Set(ref upperPlans, value);
				SelectedPlanId = this.upperPlans.Count != 0 ? this.upperPlans[0] : string.Empty;
			}
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
			set
			{
				Set(ref upperPTVs, value);
				SelectedPTVId = this.upperPTVs.Count != 0 ? this.upperPTVs[0] : string.Empty;
			}
		}

		private string selectedPTVId;
		public string SelectedPTVId
		{
			get { return selectedPTVId; }
			set { Set(ref selectedPTVId, value); }
		}

		private bool isJunctionChecked;
		public bool IsJunctionChecked
		{
			get { return isJunctionChecked; }
			set { Set(ref isJunctionChecked, value); }
		}

		private bool isControlChecked;
		public bool IsControlChecked
		{
			get { return isControlChecked; }
			set { Set(ref isControlChecked, value); }
		}

		private readonly ModelBase modelBase;

		public ICommand StartExecutionCommand { get; }

		private double progress;
		public double Progress
		{
			get { return progress; }
			set { Set(ref progress, value); }
		}

		public UpperViewModel(ModelBase modelBase)
		{
			this.modelBase = modelBase;
			IsJunctionChecked = true;
			IsControlChecked = true;
			StartExecutionCommand = new RelayCommand(StartExecution);
			RetrieveUpperPlans();
		}

		private async void RetrieveUpperPlans()
		{
			UpperPlans = await this.modelBase.GetPlansAsync(ModelBase.PlanType.Up);
		}

		private async void RetrieveUpperPTVs(string planId)
		{
			UpperPTVs = await this.modelBase.GetPTVsFromPlanAsync(planId);
		}

		private async void StartExecution()
		{
			ProgressBarViewModel pbViewModel = new ProgressBarViewModel("Upper-body");
			IProgress<double> progress = new Progress<double>(pbViewModel.IncrementProgress);
			IProgress<string> message = new Progress<string>(pbViewModel.UpdateMessage);
			ProgressBarWindow pbWindow = new ProgressBarWindow(pbViewModel);
			pbWindow.Show();

			bool[] checkedOptions = new bool[] { this.isJunctionChecked, this.IsControlChecked };
			int rescaleProgress = checkedOptions.Count(c => c); // count how many CheckBox are checked
			pbViewModel.NumOperations += rescaleProgress - 1; // rescale the progress bar update

			if (this.isJunctionChecked)
			{
				await this.modelBase.GenerateUpperJunctionAsync(this.selectedPlanId, this.selectedPTVId, progress, message);
				RetrieveUpperPTVs(this.selectedPlanId);
			}

			if (this.isControlChecked)
			{
				await this.modelBase.GenerateUpperControlAsync(this.selectedPlanId, this.selectedPTVId, progress, message);
			}

			pbViewModel.ResetProgress();
			pbWindow.Close();
		}
	}
}