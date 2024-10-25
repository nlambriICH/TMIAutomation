using System;
using System.Windows;
using TMIAutomation.ViewModel;

namespace TMIAutomation.View
{
    /// <summary>
    /// Interaction logic for StructureOpWindow.xaml
    /// </summary>
    public partial class StructureOpWindow : Window
    {
        public StructureOpWindow(StructureOpViewModel structureOpViewModel)
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