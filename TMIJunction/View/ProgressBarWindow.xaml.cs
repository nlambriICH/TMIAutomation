using System.Windows;
using TMIJunction.ViewModel;

namespace TMIJunction.View
{
    /// <summary>
    /// Interaction logic for ProgressBarWindow.xaml
    /// </summary>
    public partial class ProgressBarWindow : Window
    {
        public ProgressBarWindow(ProgressBarViewModel pbViewModel)
        {
            InitializeComponent();

            this.DataContext = pbViewModel;
        }
    }
}
