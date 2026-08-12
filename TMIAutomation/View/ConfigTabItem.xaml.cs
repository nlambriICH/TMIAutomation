using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace TMIAutomation.View
{
    /// <summary>
    /// Interaction logic for ConfigTabItem.xaml
    /// </summary>
    public partial class ConfigTabItem : UserControl
    {
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        public ConfigTabItem()
        {
            InitializeComponent();
        }
    }
}
