using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace TMIAutomation
{
    static class WindowHelper
    {
        [DllImport("user32.dll", EntryPoint = "FindWindow", SetLastError = true)]
        static extern IntPtr FindWindowByCaption(IntPtr ZeroOnly, string lpWindowName);

        [DllImport("user32.Dll")]
        static extern int PostMessage(IntPtr hWnd, UInt32 msg, int wParam, int lParam);

        private const UInt32 WM_CLOSE = 0x0010;

        public static void ShowAutoClosingMessageBox(string message, string caption)
        {
            System.Timers.Timer timer = new System.Timers.Timer(8000) { AutoReset = false };
            timer.Elapsed += delegate
            {
                IntPtr hWnd = FindWindowByCaption(IntPtr.Zero, caption);
                if (hWnd.ToInt32() != 0) PostMessage(hWnd, WM_CLOSE, 0, 0);
            };
            timer.Enabled = true;
            Thread thread = new Thread(() => MessageBox.Show(message, caption));
            thread.Start();
        }
    }
}
