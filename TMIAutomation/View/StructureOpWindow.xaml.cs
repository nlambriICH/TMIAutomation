using System;
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
        public RenameStructureWindow(StructureOpViewModel structureOpViewModel)
        {
            InitializeComponent();
            this.DataContext = structureOpViewModel;
        }

        private void Button_Click_Rename(object sender, RoutedEventArgs e)
        {
            StructureOpViewModel structureOpViewModel = (StructureOpViewModel)this.DataContext;
            structureOpViewModel.Operation = Operation.Rename;
            this.Close();
        }

        private void Button_Click_Remove(object sender, RoutedEventArgs e)
        {
            StructureOpViewModel structureOpViewModel = (StructureOpViewModel)this.DataContext;
            structureOpViewModel.Operation = Operation.Remove;
            this.Close();
        }

        private void StructureIdTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RenameStructureIdButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Activate();
            StructureIdTextBox.Focus();

            // Defer execution of CaretIndex set to ensure the UI has finished initializing
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StructureIdTextBox.CaretIndex = StructureIdTextBox.Text.Length;
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }
}