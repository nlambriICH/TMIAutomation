using System.Windows;
using System.Windows.Controls;

namespace TMIAutomation.Runner
{
    internal partial class MainWindow : Window
    {
        public MainWindow(MainViewModel mainViewModel)
        {
            InitializeComponent();
            DataContext = mainViewModel;
        }

        private void PlansAndPlanSums_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Prevent selection for this ListBox
            var listBox = (ListBox)sender;
            listBox.UnselectAll();
        }
    }
}