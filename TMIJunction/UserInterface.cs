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
            ((ComboBox)sender).ItemsSource = ((UserInterfaceModel)DataContext).LatestCourse.PlanSetups.Where(p => p.Id.Contains("up"))
                .OrderByDescending(p => p.CreationDateTime)
                .Select(p => p.Id);
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
            PlanSetup selectedBodyPlan = ((UserInterfaceModel)DataContext).LatestCourse.PlanSetups.FirstOrDefault(p => p.Id == selectedBodyPlanId);
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
            WindowHelper.ShowAutoClosingMessageBox("Generating Control Structures.\n\nPlease wait... We're working for you 😊", "TMIJunction");
            control.Create(context);
        }

        private void GenerateJunction(IStructure junction)
        {
            WindowHelper.ShowAutoClosingMessageBox("Generating Junction Structures.\n\nPlease wait... We're working for you 😊", "TMIJunction");
            junction.Create(context);
        }

        private void LegsPlanComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ((ComboBox)sender).ItemsSource = ((UserInterfaceModel)DataContext).LatestCourse.PlanSetups.Where(p => p.Id.Contains("down"))
                                                                                                      .OrderByDescending(p => p.CreationDateTime)
                                                                                                      .Select(p => p.Id);
            ((ComboBox)sender).SelectedIndex = 0;
            ((UserInterfaceModel)DataContext).LegsPlanId = ((ComboBox)sender).SelectedItem as string;
        }

        private void LegsPlanComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((UserInterfaceModel)DataContext).LegsPlanId = ((ComboBox)sender).SelectedItem as string;
        }

        private void LegsPTVComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            string selectedLegsPlanId = ((UserInterfaceModel)DataContext).LegsPlanId;
            UpdateLegsPTVIds(selectedLegsPlanId);
        }

        private void UpdateLegsPTVIds(string selectedLegsPlanId)
        {
            try
            {
                StructureSet ssInContext = string.IsNullOrEmpty(selectedLegsPlanId) ?
                    context.StructureSet : ((UserInterfaceModel)DataContext).LatestCourse.PlanSetups.FirstOrDefault(ps => ps.Id == selectedLegsPlanId).StructureSet;
                
                if (ssInContext == null)
                {
                    throw new InvalidOperationException("There is no structure set opened in the current context");
                }

                if (ssInContext == context.PlansInScope.FirstOrDefault(ps => ps.Id == ((UserInterfaceModel)DataContext).BodyPlanId)?.StructureSet)
                {
                    throw new InvalidOperationException("The structure set opened in the current context is assigned to the upper plan.\n\nPlease open a structure set for the lower extremities."); ;
                }

                LegsPTVComboBox.ItemsSource = new ObservableCollection<string>(ssInContext.Structures
                            .Where(s => s.DicomType == "PTV")
                            .OrderByDescending(s => s.Volume)
                            .Select(s => s.Id));
                LegsPTVComboBox.SelectedIndex = 0;
                ((UserInterfaceModel)DataContext).LegsPlanPTVs = LegsPTVComboBox.SelectedItem as string;
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
            PlanSetup upperPlan = ((UserInterfaceModel)DataContext).LatestCourse.PlanSetups.FirstOrDefault(ps => ps.Id == selectedBodyPlanId);
            string selectedLegsPTVId = ((UserInterfaceModel)DataContext).LegsPlanPTVs;
            string selectedRegistration = ((UserInterfaceModel)DataContext).Registration;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;

                if (LowerJunction.IsChecked == true && LowerControl.IsChecked == true)
                {
                    ExternalPlanSetup lowerPlan = GenerateNewLowerPlan();
                    GenerateJunction(new LegsJunction(upperPlan, lowerPlan, selectedLegsPTVId, selectedRegistration));

                    GenerateControl(new LegsControlStructures(lowerPlan, StructureHelper.PTV_TOTAL));
                    UpdateLegsPTVIds(lowerPlan.Id);

                    if (LowerOptimization.IsChecked == true)
                    {
                        Optimize(lowerPlan.Id);
                    }

                    MessageBox.Show("Done!", "Info");
                }
                else if (LowerJunction.IsChecked == true && LowerControl.IsChecked != true)
                {
                    ExternalPlanSetup plan = GenerateNewLowerPlan();
                    GenerateJunction(new LegsJunction(upperPlan, plan, selectedLegsPTVId, selectedRegistration));
                    UpdateLegsPTVIds(plan.Id);

                    if (LowerOptimization.IsChecked == true)
                    {
                        Optimize(plan.Id);
                    }

                    MessageBox.Show("Done!", "Info");
                }
                else if (LowerJunction.IsChecked != true && LowerControl.IsChecked == true)
                {
                    ExternalPlanSetup plan = GenerateNewLowerPlan();
                    GenerateControl(new LegsControlStructures(plan, selectedLegsPTVId));

                    if (LowerOptimization.IsChecked == true)
                    {
                        Optimize(plan.Id);
                    }

                    MessageBox.Show("Done!", "Info");
                }
                else if (LowerJunction.IsChecked != true && LowerControl.IsChecked != true && LowerOptimization.IsChecked == true)
                {
                    string selectedLegsPlanId = ((UserInterfaceModel)DataContext).LegsPlanId;

                    Optimize(selectedLegsPlanId);

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

        private ExternalPlanSetup GenerateNewLowerPlan()
        {
            if (context.StructureSet == null) throw new InvalidOperationException("Plase open the structure set to be assigned to the new plan.");

            ExternalPlanSetup plan = ((UserInterfaceModel)DataContext).LatestCourse.AddExternalPlanSetup(context.StructureSet);

            int numOfAutoPlans = ((UserInterfaceModel)DataContext).LatestCourse.PlanSetups.Count(ps => ps.Id.Contains("TMLIdownAuto"));
            plan.Id = numOfAutoPlans == 0 ? "TMLIdownAuto" : string.Concat("TMLIdownAuto", numOfAutoPlans);
            UpdateLegsPlanIds();
            return plan;
        }

        private void Optimize(string planId)
        {
            string selectedMachine = ((UserInterfaceModel)DataContext).MachineName;

            Isocenter iso = new Isocenter(((UserInterfaceModel)DataContext).LatestCourse, planId, selectedMachine);
            iso.Set(context);

            WindowHelper.ShowAutoClosingMessageBox($"Isocenters placed.\n\nStart optimization...\n\nCheck the execution status at: {LoggerHelper.LogDirectory}", "Info", time: 30000);

            Optimization opt = new Optimization(planId);
            opt.Start(context);
        }

        private void UpdateLegsPlanIds()
        {
            LegsPlanComboBox.ItemsSource = new ObservableCollection<string>(context.PlansInScope
                                            .OrderByDescending(p => p.CreationDateTime)
                                            .Select(p => p.Id));
            LegsPlanComboBox.SelectedIndex = 0;
            ((UserInterfaceModel)DataContext).LegsPlanId = LegsPlanComboBox.SelectedItem as string;
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

        private void Machine_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((UserInterfaceModel)DataContext).LegsPlanId = ((ComboBox)sender).SelectedItem as string;
        }

        private void MachineComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            ((ComboBox)sender).ItemsSource = new string[] { "TrueBeamSN1015", "TrueBeamSN4791" };
            ((ComboBox)sender).SelectedIndex = 0;
            ((UserInterfaceModel)DataContext).MachineName = ((ComboBox)sender).SelectedItem as string;
        }

        private void MachineComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ((UserInterfaceModel)DataContext).MachineName = ((ComboBox)sender).SelectedItem as string;
        }

    }
}
