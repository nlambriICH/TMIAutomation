using System.Windows;
using TMIAutomation.ViewModel;

namespace TMIAutomation.View
{
    /// <summary>
    /// Interaction logic for OARSelection.xaml
    /// </summary>
    public partial class OARSelection : Window
    {
        public OARSelection(OARSelectionViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
        }

        private void Button_Click_Confirm(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
