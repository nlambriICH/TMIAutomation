using System.Windows;
using Serilog;
using TMIAutomation.ViewModel;
using System.Threading;

namespace TMIAutomation.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            SetLanguageDictionary();
            this.DataContext = viewModel;
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            Log.CloseAndFlush();
        }

        private void SetLanguageDictionary()
        {
            switch (Thread.CurrentThread.CurrentCulture.ToString())
            {
                case "it-IT":
                    TMIAutomation.Language.Resources.Culture = new System.Globalization.CultureInfo("it-IT");
                    break;
                default:
                    TMIAutomation.Language.Resources.Culture = new System.Globalization.CultureInfo("en");
                    break;
            }
        }
    }
}