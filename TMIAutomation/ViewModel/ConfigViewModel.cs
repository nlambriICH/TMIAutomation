using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;

namespace TMIAutomation.ViewModel
{
    public class ConfigViewModel : ViewModelBase
    {
        private readonly ModelBase modelBase;
        public ICommand ReloadConfigurationCommand { get; }
        public ICommand OpenConfigurationCommand { get; }

        private string dosePerFraction = ConfigOptOptions.DosePerFraction;
        public string DosePerFraction
        {
            get => dosePerFraction;
            set => Set(ref dosePerFraction, value);
        }

        private string numberOfFractions = ConfigOptOptions.NumberOfFractions;
        public string NumberOfFractions
        {
            get => numberOfFractions;
            set => Set(ref numberOfFractions, value);
        }

        private string treatmentMachine = ConfigOptOptions.TreatmentMachine;
        public string TreatmentMachine
        {
            get => treatmentMachine;
            set => Set(ref treatmentMachine, value);
        }

        private string mlcId = ConfigOptOptions.MLCID;
        public string MLCId
        {
            get => mlcId;
            set => Set(ref mlcId, value);
        }

        private string energy = ConfigOptOptions.Energy;
        public string Energy
        {
            get => energy;
            set => Set(ref energy, value);
        }

        private int doseRate = ConfigOptOptions.DoseRate;
        public int DoseRate
        {
            get => doseRate;
            set => Set(ref doseRate, value);
        }

        private string optimizationAlgorithm = ConfigOptOptions.OptimizationAlgorithm;
        public string OptimizationAlgorithm
        {
            get => optimizationAlgorithm;
            set => Set(ref optimizationAlgorithm, value);
        }

        private string doseAlgorithm = ConfigOptOptions.DoseAlgorithm;
        public string DoseAlgorithm
        {
            get => doseAlgorithm;
            set => Set(ref doseAlgorithm, value);
        }
     
        public string PlanningLabel { get; } =
#if ESAPI15
        "Base dose planning";
#else
        "Auto planning";
#endif

        public string TooltipPlanningLabel { get; } =
#if ESAPI15
        "True: create the base-dose plan; False: use junction structures to optimize the lower plan";
#else
        "True: setup and run the optimization of the lower plan; False: setup the lower plan";
#endif

        private bool selectedPlanningOption =
#if ESAPI15
            ConfigOptOptions.BaseDosePlanning;
#else
            ConfigOptOptions.AutoPlanLowerExtremities;
#endif
        public bool SelectedPlanningOption
        {
            get => selectedPlanningOption;
            set => Set(ref selectedPlanningOption, value);
        }

        private string lowerExtremitiesCollimator = ConfigOptOptions.LowerExtremitiesCollimator;
        public string LowerExtremitiesCollimator
        {
            get => lowerExtremitiesCollimator;
            set => Set(ref lowerExtremitiesCollimator, value);
        }

        public ConfigViewModel(ModelBase modelBase)
        {
            this.modelBase = modelBase;
            ReloadConfigurationCommand = new RelayCommand(ReloadConfiguration);
            OpenConfigurationCommand = new RelayCommand(OpenConfiguration);
        }

        private void ReloadConfiguration()
        {
            ConfigOptOptions.Init();
            DosePerFraction = ConfigOptOptions.DosePerFraction;
            NumberOfFractions = ConfigOptOptions.NumberOfFractions;
            TreatmentMachine = ConfigOptOptions.TreatmentMachine;
            MLCId = ConfigOptOptions.MLCID;
            Energy = ConfigOptOptions.Energy;
            DoseRate = ConfigOptOptions.DoseRate;
            OptimizationAlgorithm = ConfigOptOptions.OptimizationAlgorithm;
            DoseAlgorithm = ConfigOptOptions.DoseAlgorithm;
#if ESAPI15
            SelectedPlanningOption = ConfigOptOptions.BaseDosePlanning;
#else
            SelectedPlanningOption = ConfigOptOptions.AutoPlanLowerExtremities;
#endif
            LowerExtremitiesCollimator = ConfigOptOptions.LowerExtremitiesCollimator;
            RaisePropertyChanged(string.Empty);
        }
        private void OpenConfiguration()
        {
            if (File.Exists(ConfigOptOptions.OptOptionsPath))
            {
                Process.Start(new ProcessStartInfo(ConfigOptOptions.OptOptionsPath) { UseShellExecute = true });
            }
        }
    }
}
