using System;
using System.Windows;

namespace TMIAutomation.View
{
    /// <summary>
    /// Interaction logic for LowerPlanOptSelection.xaml
    /// </summary>
    public partial class LowerPlanOptSelection : Window
    {
        private bool? generateBaseDosePlanOnly = null;

        public LowerPlanOptSelection()
        {
            InitializeComponent();
        }

        private void Button_Click_Junction(object sender, RoutedEventArgs e)
        {
            this.generateBaseDosePlanOnly = false;
            this.Close();
        }

        private void Button_Click_BaseDose(object sender, RoutedEventArgs e)
        {
            this.generateBaseDosePlanOnly = true;
            this.Close();
        }

        public bool? GenerateBaseDosePlanOnly()
        {
            return generateBaseDosePlanOnly;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (generateBaseDosePlanOnly == null)
            {
                MessageBox.Show("The whole optimization will be performed using the junction substructures strategy.",
                    "Lower-extremities optimization",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
