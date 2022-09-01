using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;

namespace TMIJunction.ViewModel
{
    public sealed class ProgressBarViewModel : ViewModelBase
    {
        private double progress;
        public double Progress
        {
            get { return progress; }
            set { Set(ref progress, value); }
        }

        private string message;
        public string Message
        {
            get { return message; }
            set { Set(ref message, value); }
        }

        private string windowTitle;
        public string WindowTitle
        {
            get { return windowTitle; }
            set { Set(ref windowTitle, value); }
        }

        public ProgressBarViewModel(string windowTitle)
        {
            WindowTitle = windowTitle;
        }

        public void UpdateProgress(double p)
        {
            Progress = p;
        }

        public void ResetProgress()
        {
            UpdateProgress(0.0);
        }

        public void UpdateMessage(string m)
        {
            Message = m;
        }
    }
}
