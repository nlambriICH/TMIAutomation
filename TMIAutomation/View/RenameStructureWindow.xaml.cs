using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TMIAutomation.ViewModel;

namespace TMIAutomation.View
{
    /// <summary>
    /// Interaction logic for AddStructureWindow.xaml
    /// </summary>
    public partial class RenameStructureWindow : Window
    {
        public RenameStructureWindow(RenameStructureViewModel addStructureViewModel)
        {
            InitializeComponent();
            this.DataContext = addStructureViewModel;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void StructureIdTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ConfirmStructureIdButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Activate();
            StructureIdTextBox.Focus();
        }
    }
}
