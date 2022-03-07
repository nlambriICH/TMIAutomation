using TMIAutomation;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using VMS.TPS.Common.Model.API;
using TMIAutomation.Model;
using System;
using System.Linq;

// TODO: Replace the following version attributes by creating AssemblyInfo.cs. You can do this in the properties of the Visual Studio project.
[assembly: AssemblyVersion("1.0.0.1")]
[assembly: AssemblyFileVersion("1.0.0.1")]
[assembly: AssemblyInformationalVersion("1.0")]

// TODO: Uncomment the following line if the script requires write access.
[assembly: ESAPIScript(IsWriteable = true)]

namespace VMS.TPS
{
    public class Script
    {

        public Script()
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Execute(ScriptContext context, Window window /*, ScriptEnvironment environment*/)
        {
            try
            {
                if (context.PlansInScope == null && context.PlansInScope.Any())
                {
                    MessageBox.Show("No plans opened in Eclipse.");
                    return;
                }

                UserInterface ui = new UserInterface(context)
                {
                    DataContext = new UserInterfaceModel()
                };
                window.Content = ui;
                window.SizeToContent = SizeToContent.WidthAndHeight;
                window.ResizeMode = ResizeMode.NoResize;
                window.Title = "ESAPI";
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }

        }
    }
}
