using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
