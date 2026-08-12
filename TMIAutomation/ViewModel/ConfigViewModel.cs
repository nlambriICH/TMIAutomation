using System.Collections.Generic;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;

namespace TMIAutomation.ViewModel
{
    public class ConfigViewModel : ViewModelBase
    {
        private readonly ModelBase modelBase;

        public ICommand UpdateConfigurationCommand { get; }
        public ICommand OpenConfigurationCommand { get; }

        private bool isPlanningOptionSelected; // AutoPlanLowerExtremities for ESPAI > 15 or BaseDosePlanning

        public string PlanningLabel { get; } =
#if ESAPI18
        "Auto plan lower extremities";
#else
        "Base dose planning";
#endif
        public string TooltipPlanningLabel { get; } =
#if ESAPI18
        "Yes: setup and run the optimization of the lower plan; No: setup the lower plan";
#else
        "Yes: create the base-dose plan; No: use junction structures to optimize the lower plan";
#endif

        public bool IsPlanningOptionSelected
        {
            get => isPlanningOptionSelected;
            set
            {
                if (isPlanningOptionSelected != value)
                {
                    Set(ref isPlanningOptionSelected, value);
                }
            }
        }

        public List<string> CollAngleOptions { get; } = new List<string> { "5 deg", "5/355 deg" };

        private string selectedCollAngleOption = "5 deg"; // Default choice

        public string SelectedCollAngleOption
        {
            get => selectedCollAngleOption;
            set => Set(ref selectedCollAngleOption, value);
        }

        public ConfigViewModel(ModelBase modelBase)
        {
            this.modelBase = modelBase;
            UpdateConfigurationCommand = new RelayCommand(UpdateConfiguration);
            OpenConfigurationCommand = new RelayCommand(OpenConfiguration);
        }

        private async void UpdateConfiguration() { }
        private void OpenConfiguration() { System.Diagnostics.Process.Start(ConfigOptOptions.OptOptionsPath);  }
    }
}
