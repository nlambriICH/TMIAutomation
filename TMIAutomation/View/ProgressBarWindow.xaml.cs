using Serilog;
using System;
using System.Windows;
using TMIAutomation.ViewModel;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation.View
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

            Log.Logger = new LoggerConfiguration()
#if DEBUG
                    .MinimumLevel.Verbose()
#else
                    .MinimumLevel.Debug()
#endif
                    .Destructure.ByTransforming<VVector>(vv => new
                    {
                        X = Math.Round(vv.x, 1, MidpointRounding.AwayFromZero),
                        Y = Math.Round(vv.y, 1, MidpointRounding.AwayFromZero),
                        Z = Math.Round(vv.z, 1, MidpointRounding.AwayFromZero)
                    })
                    .Destructure.ByTransforming<VRect<double>>(vr => new
                    {
                        X1 = Math.Round(vr.X1, 1, MidpointRounding.AwayFromZero),
                        X2 = Math.Round(vr.X2, 1, MidpointRounding.AwayFromZero),
                        Y1 = Math.Round(vr.Y1, 1, MidpointRounding.AwayFromZero),
                        Y2 = Math.Round(vr.Y2, 1, MidpointRounding.AwayFromZero)
                    })
                .WriteTo.Logger(Log.Logger)
                .WriteTo.RichTextBox(TMIAutomationLogs)
                .CreateLogger();
        }

        private void TMIAutomationLogs_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            TMIAutomationLogs.ScrollToEnd();
        }
    }
}
