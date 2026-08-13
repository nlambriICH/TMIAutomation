using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using TMIAutomation.Async;

namespace TMIAutomation.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        public UpperViewModel UpperVM { get; }
        public LowerViewModel LowerVM { get; }
        public ScheduleViewModel ScheduleVM { get; }
        public ConfigViewModel ConfigVM { get; }

        public ICommand OpenDocumentationCommand { get; }

        public MainViewModel(EsapiWorker esapiWorker)
        {
            ModelBase modelBase = new ModelBase(esapiWorker);
            UpperVM = new UpperViewModel(modelBase);
            LowerVM = new LowerViewModel(modelBase);
            ScheduleVM = new ScheduleViewModel(modelBase);
            ConfigVM = new ConfigViewModel(modelBase);
            OpenDocumentationCommand = new RelayCommand(OpenDocumentation);

        }

        private void OpenDocumentation()
        {
            try
            {
                string pdfPath = GetDocumentationPath("TMIAutomation.pdf");

                Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open documentation:\n{ex.Message}",
                                "File Not Found",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private string GetDocumentationPath(string fileName)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // 1. Check directly relative to executable (Deployment / Release output)
            string localPath = Path.Combine(assemblyDir, "Docs", fileName);
            if (File.Exists(localPath))
            {
                return localPath;
            }

            // 2. Walk up parent directories to locate the root "Docs" folder (Development mode)
            DirectoryInfo currentDir = new DirectoryInfo(assemblyDir);
            while (currentDir != null)
            {
                string candidatePath = Path.Combine(currentDir.FullName, "Docs", fileName);
                if (File.Exists(candidatePath))
                {
                    return candidatePath;
                }
                currentDir = currentDir.Parent;
            }

            throw new FileNotFoundException($"Could not locate '{fileName}' in any parent 'Docs' directory.");
        }
    }
}