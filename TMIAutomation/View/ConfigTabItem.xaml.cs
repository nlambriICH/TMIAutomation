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
        // Helper to construct what the TextBox text will be if the keypress is allowed
        private string GetProposedText(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                string currentText = textBox.Text;
                int selectionStart = textBox.SelectionStart;
                int selectionLength = textBox.SelectionLength;

                return currentText.Remove(selectionStart, selectionLength)
                                  .Insert(selectionStart, e.Text);
            }
            return string.Empty;
        }

        // 1. Allows positive integers only (e.g., 1, 10, 25)
        private void PositiveIntegerValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            string proposedText = GetProposedText(sender, e);
            Regex regex = new Regex(@"^\d+$");
            e.Handled = !regex.IsMatch(proposedText);
        }

        // 2. Allows positive numbers with at most two decimal places (e.g., 1, 1.2, 1.25, .5)
        private void PositiveDecimalValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            string proposedText = GetProposedText(sender, e);
            // Accepts '.' or ',' depending on decimal formatting preference
            Regex regex = new Regex(@"^\d*([\.,]\d{0,2})?$");
            e.Handled = !regex.IsMatch(proposedText);
        }

        public ConfigTabItem()
        {
            InitializeComponent();
        }
    }
}
