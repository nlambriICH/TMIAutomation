using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using System.Windows.Navigation;
using System.Windows.Shapes;
using TMIAutomation.Model;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class UserInterface : UserControl
    {
        private readonly ScriptContext context;

        public UserInterface(ScriptContext context)
        {
            InitializeComponent();
            this.context = context;
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
			((UserInterfaceModel)DataContext).BodyPlanPTVs = new ObservableCollection<string>(context.PlansInScope.FirstOrDefault(p => p.Id == selectedBodyPlanId).StructureSet.Structures
							.Where(s => s.DicomType == "PTV")
							.OrderByDescending(s => s.Volume)
							.Select(p => p.Id));
            BodyPTVComboBox.ItemsSource = ((UserInterfaceModel)DataContext).BodyPlanPTVs;
            BodyPTVComboBox.SelectedIndex = 0;
        }

		private void BodyJunctionButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedBodyPlanId = ((UserInterfaceModel)DataContext).BodyPlanId;
            string selectedBodyPTVId = ((UserInterfaceModel)DataContext).BodyPlanPTVs.FirstOrDefault();
            IStructure bodyJunction = new BodyJunction(selectedBodyPlanId, selectedBodyPTVId);
			try
			{
				Mouse.OverrideCursor = Cursors.Wait;
				bodyJunction.Create(context);
				UpdateBodyPTVIds(selectedBodyPlanId);
                MessageBox.Show("Done!", "Info");
            }
			finally
			{
                Mouse.OverrideCursor = null;
            }
        }

        private void BodyControlButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedBodyPlanId = ((UserInterfaceModel)DataContext).BodyPlanId;
            string selectedBodyPTVId = ((UserInterfaceModel)DataContext).BodyPlanPTVs.FirstOrDefault();
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                IStructure bodyControl = new BodyControlStructures(selectedBodyPlanId, selectedBodyPTVId);
                //WindowHelper.ShowAutoClosingMessageBox("Please wait... We're working for you 😊", "TMIAutomation");
                bodyControl.Create(context);
                MessageBox.Show("Done!", "Info");
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
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
            ((UserInterfaceModel)DataContext).LegsPlanPTVs = new ObservableCollection<string>(context.PlansInScope.FirstOrDefault(p => p.Id == selectedLegsPlanId).StructureSet.Structures
                            .Where(s => s.DicomType == "PTV")
                            .OrderByDescending(s => s.Volume)
                            .Select(p => p.Id));
            LegsPTVComboBox.ItemsSource = ((UserInterfaceModel)DataContext).LegsPlanPTVs;
            LegsPTVComboBox.SelectedIndex = 0;
        }

		private void RegistrationComboBox_Loaded(object sender, RoutedEventArgs e)
		{
            string selectedBodyPlanId = ((UserInterfaceModel)DataContext).BodyPlanId;
            ((ComboBox)sender).ItemsSource = context.Patient.Registrations.OrderByDescending(reg => reg.CreationDateTime).Select(reg => reg.Id);
            ((ComboBox)sender).SelectedIndex = 0;
        }

		private void LegsJunctionButton_Click(object sender, RoutedEventArgs e)
		{
            string selectedBodyPlanId = ((UserInterfaceModel)DataContext).BodyPlanId;
            string selectedLegsPlanId = ((UserInterfaceModel)DataContext).LegsPlanId;
            string selectedLegsPTVId = ((UserInterfaceModel)DataContext).LegsPlanPTVs.FirstOrDefault();
            string selectedRegistration = ((UserInterfaceModel)DataContext).Registration;
			try
			{
				IStructure legsJunction = new LegsJunction(selectedBodyPlanId, selectedLegsPlanId, selectedLegsPTVId, selectedRegistration);
				Mouse.OverrideCursor = Cursors.Wait;
				//WindowHelper.ShowAutoClosingMessageBox("Please wait... We're working for you 😊", "TMIAutomation");
				legsJunction.Create(context);
				UpdateLegsPTVIds(selectedLegsPlanId);
                MessageBox.Show("Done!", "Info");
			}
			finally
			{
                Mouse.OverrideCursor = Cursors.Arrow;
            }
        }

		private void LegsControlButton_Click(object sender, RoutedEventArgs e)
		{
            string selectedLegsPlanId = ((UserInterfaceModel)DataContext).LegsPlanId;
            string selectedLegsPTVId = ((UserInterfaceModel)DataContext).LegsPlanPTVs.FirstOrDefault();
			try
			{
				IStructure legscontrol = new LegsControlStructures(selectedLegsPlanId, selectedLegsPTVId);
				Mouse.OverrideCursor = Cursors.Wait;
				//WindowHelper.ShowAutoClosingMessageBox("Please wait... We're working for you 😊", "TMIAutomation");
				legscontrol.Create(context);
                MessageBox.Show("Done!", "Info");
            }
			finally
			{
                Mouse.OverrideCursor = null;
            }
        }

		private void RegistrationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
            ((UserInterfaceModel)DataContext).Registration = ((ComboBox)sender).SelectedItem as string;
        }
	}
}
