using System.Windows;
using TMIAutomation.ViewModel;

namespace TMIAutomation.View
{
    /// <summary>
    /// Interaction logic for OARSelection.xaml
    /// </summary>
    public partial class OARSelection : Window
    {
        private bool buttonClickClose;
        public bool userClosing;

        public OARSelection(OARSelectionViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            Closing += OARSelection_Closing;
        }

        private void OARSelection_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!buttonClickClose)
            {
                userClosing = true;
            }
        }

        private void Button_Click_Confirm(object sender, RoutedEventArgs e)
        {
            this.Close();
            this.buttonClickClose = true;
        }
    }
}
