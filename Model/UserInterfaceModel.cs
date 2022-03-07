using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace TMIAutomation.Model
{
    class UserInterfaceModel : INotifyPropertyChanged
    {

        private string bodyPlanId;

        public string BodyPlanId
        {
            get { return bodyPlanId; }
            set { bodyPlanId = value; }
        }

		private string legsPlanId;

		public string LegsPlanId
		{
			get { return legsPlanId; }
			set { legsPlanId = value; }
		}

        private string registration;

        public string Registration
        {
            get { return registration; }
            set
            {
                registration = value;
                OnPropertyChanged(nameof(Registration));
            }
        }


        private ObservableCollection<string> legsPlanPTVs;
        private ObservableCollection<string> bodyPlanPTVs;
       
		public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<string> BodyPlanPTVs
        {
            get { return bodyPlanPTVs; }
            set
            {
                bodyPlanPTVs = value;
                OnPropertyChanged("BodyPTVComboBox");
            }
        }

        public ObservableCollection<string> LegsPlanPTVs
        {
            get { return legsPlanPTVs; }
            set
            {
                legsPlanPTVs = value;
                OnPropertyChanged("LegsPTVComboBox");
            }
        }

        private void OnPropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
