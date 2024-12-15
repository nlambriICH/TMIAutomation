using System.Windows;
using TMIAutomation.ViewModel;

namespace TMIAutomation.View
{
    /// <summary>
    /// Interaction logic for StructureSelection.xaml
    /// </summary>
    public partial class StructureSetSelection : Window
    {
        private bool buttonClickClose;
        public bool userClosing;

        public StructureSetSelection(StructureSetSelectionViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            Closing += StructureSetSelection_Closing;
        }

        private void StructureSetSelection_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!buttonClickClose)
            {
                userClosing = true;
            }
        }

        private void Button_Click_Confirm(object sender, RoutedEventArgs e)
        {
            this.buttonClickClose = true;
            this.Close();
        }
    }
}
