using Serilog;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VMS.TPS.Common.Model.API;

namespace TMIJunction
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class UserInterface : UserControl
    {
        private readonly ScriptContext context;
        private readonly ILogger logger;

        public UserInterface(ScriptContext context)
        {
            InitializeComponent();
            this.context = context;
            this.logger = Log.ForContext<UserInterface>();
        }

        private void BodyPlanComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ((ComboBox)sender).ItemsSource = context.PlansInScope.Where(p => p.Id.Contains("up")).OrderByDescending(p => p.CreationDateTime).Select(p => p.Id);
            ((ComboBox)sender).SelectedIndex = 0;
            ((UserInterfaceModel)DataContext).BodyPlanId = ((ComboBox)sender).SelectedItem as string;
        }

        private void BodyPlanComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedBodyPlanId = ((ComboBox)sender).SelectedItem as string;
            ((UserInterfaceModel)DataContext).BodyPlanId = selectedBodyPlanId;
            UpdateBodyPTVIds(selectedBodyPlanId);
        }

        private void BodyPTVComboBox_Loaded(object sender, RoutedEventArgs e)
		{
			string selectedBodyPlanId = ((UserInterfaceModel)DataContext).BodyPlanId;
			UpdateBodyPTVIds(selectedBodyPlanId);
		}

		private void UpdateBodyPTVIds(string selectedBodyPlanId)
		{
            PlanSetup selectedBodyPlan = context.PlansInScope.FirstOrDefault(p => p.Id == selectedBodyPlanId);
            try
            {
                if (selectedBodyPlan != null)
                {
                    BodyPTVComboBox.ItemsSource = new ObservableCollection<string>(selectedBodyPlan.StructureSet.Structures
                                .Where(s => s.DicomType == "PTV")
                                .OrderByDescending(s => s.Volume)
                                .Select(p => p.Id));
                    BodyPTVComboBox.SelectedIndex = 0;
                    ((UserInterfaceModel)DataContext).BodyPlanPTVs = BodyPTVComboBox.SelectedItem as string;
                }
            }
            catch (Exception exc)
            {
                logger.LogAndWarnException(exc);
            }
        }

		private void UpperButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedBodyPlanId = ((UserInterfaceModel)DataContext).BodyPlanId;
            string selectedBodyPTVId = ((UserInterfaceModel)DataContext).BodyPlanPTVs;
			try
			{
                if (UpperJunction.IsChecked == true && UpperControl.IsChecked == true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    GenerateJunction(new BodyJunction(selectedBodyPlanId, selectedBodyPTVId));
                    GenerateControl(new BodyControlStructures(selectedBodyPlanId, StructureHelper.PTV_TOTAL));
                    UpdateBodyPTVIds(selectedBodyPlanId);

                    MessageBox.Show("Done!", "Info");
                }
                else if (UpperJunction.IsChecked == true && UpperControl.IsChecked != true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    GenerateJunction(new BodyJunction(selectedBodyPlanId, selectedBodyPTVId));
                    UpdateBodyPTVIds(selectedBodyPlanId);

                    MessageBox.Show("Done!", "Info");
                }
                else if (UpperJunction.IsChecked != true && UpperControl.IsChecked == true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    GenerateControl(new BodyControlStructures(selectedBodyPlanId, selectedBodyPTVId));

                    MessageBox.Show("Done!", "Info");
                }
            }
            catch (Exception exc)
            {
                logger.LogAndWarnException(exc);
            }
            finally
			{
                Mouse.OverrideCursor = null;
            }
        }

        private void GenerateControl(IStructure control)
        {
            WindowHelper.ShowAutoClosingMessageBox("Generating Control Structures.\nPlease wait... We're working for you 😊", "TMIJunction");
            control.Create(context);
        }

        private void GenerateJunction(IStructure junction)
        {
            WindowHelper.ShowAutoClosingMessageBox("Generating Junction Structures.\nPlease wait... We're working for you 😊", "TMIJunction");
            junction.Create(context);
        }

        private void LegsPlanComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ((ComboBox)sender).ItemsSource = context.PlansInScope.Where(p => p.Id.Contains("down")).OrderByDescending(p => p.CreationDateTime).Select(p => p.Id);
            ((ComboBox)sender).SelectedIndex = 0;
            ((UserInterfaceModel)DataContext).LegsPlanId = ((ComboBox)sender).SelectedItem as string;
        }

        private void LegsPlanComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((UserInterfaceModel)DataContext).LegsPlanId = ((ComboBox)sender).SelectedItem as string;
            UpdateLegsPTVIds(((UserInterfaceModel)DataContext).LegsPlanId);
        }

        private void LegsPTVComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            string selectedLegsPlanId = ((UserInterfaceModel)DataContext).LegsPlanId;
            UpdateLegsPTVIds(selectedLegsPlanId);
        }

        private void UpdateLegsPTVIds(string selectedLegsPlanId)
        {
            PlanSetup selectedLegsPlan = context.PlansInScope.FirstOrDefault(p => p.Id == selectedLegsPlanId);
            try
            {
                if (selectedLegsPlan != null)
                {
                    LegsPTVComboBox.ItemsSource = new ObservableCollection<string>(selectedLegsPlan.StructureSet.Structures
                                .Where(s => s.DicomType == "PTV")
                                .OrderByDescending(s => s.Volume)
                                .Select(p => p.Id));
                    LegsPTVComboBox.SelectedIndex = 0;
                    ((UserInterfaceModel)DataContext).LegsPlanPTVs = LegsPTVComboBox.SelectedItem as string;
                }
            }
            catch (Exception exc)
            {
                logger.LogAndWarnException(exc);
            }
        }

		private void RegistrationComboBox_Loaded(object sender, RoutedEventArgs e)
		{
            string selectedBodyPlanId = ((UserInterfaceModel)DataContext).BodyPlanId;
            ((ComboBox)sender).ItemsSource = context.Patient.Registrations.OrderByDescending(reg => reg.CreationDateTime).Select(reg => reg.Id);
            ((ComboBox)sender).SelectedIndex = 0;
        }

		private void LowerButton_Click(object sender, RoutedEventArgs e)
		{
            string selectedBodyPlanId = ((UserInterfaceModel)DataContext).BodyPlanId;
            string selectedLegsPlanId = ((UserInterfaceModel)DataContext).LegsPlanId;
            string selectedLegsPTVId = ((UserInterfaceModel)DataContext).LegsPlanPTVs;
            string selectedRegistration = ((UserInterfaceModel)DataContext).Registration;
            try
            {
                if (LowerJunction.IsChecked == true && LowerControl.IsChecked == true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    GenerateJunction(new LegsJunction(selectedBodyPlanId, selectedLegsPlanId, selectedLegsPTVId, selectedRegistration));

                    GenerateControl(new LegsControlStructures(selectedLegsPlanId, StructureHelper.PTV_TOTAL));
                    UpdateLegsPTVIds(selectedLegsPlanId);

                    MessageBox.Show("Done!", "Info");
                }
                else if (LowerJunction.IsChecked == true && LowerControl.IsChecked != true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    GenerateJunction(new LegsJunction(selectedBodyPlanId, selectedLegsPlanId, selectedLegsPTVId, selectedRegistration));
                    UpdateLegsPTVIds(selectedLegsPlanId);

                    MessageBox.Show("Done!", "Info");
                }
                else if (LowerJunction.IsChecked != true && LowerControl.IsChecked == true)
                {
                    Mouse.OverrideCursor = Cursors.Wait;
                    GenerateControl(new LegsControlStructures(selectedLegsPlanId, selectedLegsPTVId));

                    MessageBox.Show("Done!", "Info");
                }
            }
            catch (Exception exc)
            {
                logger.LogAndWarnException(exc);
            }
            finally
            {
                Mouse.OverrideCursor = Cursors.Arrow;
            }
        }

		private void RegistrationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
            ((UserInterfaceModel)DataContext).Registration = ((ComboBox)sender).SelectedItem as string;
        }

        private void BodyPTVComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((UserInterfaceModel)DataContext).BodyPlanPTVs = BodyPTVComboBox.SelectedItem as string;
        }

        private void LegsPTVComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((UserInterfaceModel)DataContext).LegsPlanPTVs = LegsPTVComboBox.SelectedItem as string;
        }
    }
}
