using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;

namespace TMIAutomation.ViewModel
{
    public class ConfigViewModel : ViewModelBase, IDisposable
    {
        private readonly ModelBase modelBase;
        private readonly string configFilePath = ConfigOptOptions.OptOptionsPath;
        private FileSystemWatcher configWatcher;
        private DateTime lastFileReadTime = DateTime.MinValue;

        private string statusMessage;
        public string StatusMessage
        {
            get => statusMessage;
            set => Set(ref statusMessage, value);
        }

        private Visibility statusVisibility = Visibility.Collapsed;
        public Visibility StatusVisibility
        {
            get => statusVisibility;
            set => Set(ref statusVisibility, value);
        }

        private CancellationTokenSource statusTimerCts;

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
            InitializeFileWatcher();
            OpenConfigurationCommand = new RelayCommand(OpenConfiguration);
        }

        private void InitializeFileWatcher()
        {
            if (!File.Exists(configFilePath)) return;

            string directory = Path.GetDirectoryName(configFilePath);
            string fileName = Path.GetFileName(configFilePath);

            configWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            configWatcher.Changed += OnConfigFileChanged;
        }

        private void OnConfigFileChanged(object sender, FileSystemEventArgs e)
        {
            // 1. Debounce: Ignore duplicate events triggered within 500ms of each other
            if ((DateTime.Now - lastFileReadTime).TotalMilliseconds < 500)
            {
                return;
            }
            lastFileReadTime = DateTime.Now;

            // 2. Dispatch back to the WPF UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                ReloadWithRetry();
            });
        }

        private async void ShowTemporaryStatus(string message, int displayDurationMs = 10000)
        {
            // Cancel any running timer from a previous rapid reload
            statusTimerCts?.Cancel();
            statusTimerCts = new CancellationTokenSource();
            var token = statusTimerCts.Token;

            StatusMessage = message;
            StatusVisibility = Visibility.Visible;

            try
            {
                await Task.Delay(displayDurationMs, token);
                StatusVisibility = Visibility.Collapsed;
            }
            catch (TaskCanceledException)
            {
                // Silently handle cancellation when a newer status overrides this one
            }
        }

        private void ReloadWithRetry()
        {
            // Try up to 3 times to account for temporary file locks while the editor saves
            int maxRetries = 3;
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    // Call your static reader
                    ConfigOptOptions.Init();

                    // Update ViewModel backing fields
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

                    // Refresh all bindings on the UI
                    RaisePropertyChanged(string.Empty);

                    // Trigger the temporary notification
                    ShowTemporaryStatus($"Configuration auto-reloaded at {DateTime.Now:HH:mm:ss}");
                    break;
                }
                catch (IOException)
                {
                    // File is locked by the editor writing to it; wait briefly before retrying
                    Thread.Sleep(100);
                }
            }
        }

        private void OpenConfiguration()
        {
            if (File.Exists(configFilePath))
            {
                Process.Start(new ProcessStartInfo(configFilePath) { UseShellExecute = true });
            }
        }

        public void Dispose()
        {
            if (configWatcher != null)
            {
                configWatcher.Changed -= OnConfigFileChanged;
                configWatcher.EnableRaisingEvents = false;
                configWatcher.Dispose();
            }
        }
    }
}
