using System.Windows;
using VMS.TPS;
using VMS.TPS.Common.Model.API;

[assembly: ESAPIScript(IsWriteable = true)]

namespace TMIAutomation.Runner
{
    public partial class App : System.Windows.Application
    {
        private void OnStartup(object sender, StartupEventArgs evt)
        {
            ScriptRunner runner = new ScriptRunner(new Script());
            MainViewModel mainViewModel = new MainViewModel(runner);
            MainWindow window = new MainWindow(mainViewModel);
            window.Closed += (o, e) => runner.Dispose();
            window.Show();
        }

        public void Dummy(PlanSetup plan) { }
    }
}
