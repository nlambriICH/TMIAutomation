using Serilog;
using System.Windows;
using TMIAutomation.ViewModel;

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
            this.DataContext = viewModel;
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            Log.CloseAndFlush();
        }
    }
}
