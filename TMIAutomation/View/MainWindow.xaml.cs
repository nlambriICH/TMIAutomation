using System.Windows;
using TMIAutomation.ViewModel;

namespace TMIAutomation.View
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		public MainWindow(MainViewModel viewModel)
		{
			InitializeComponent();

			this.DataContext = viewModel;
			Closed += viewModel.MainView_Closed;
		}
	}
}
