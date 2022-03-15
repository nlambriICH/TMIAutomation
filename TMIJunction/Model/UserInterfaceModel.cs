using System.ComponentModel;

namespace TMIJunction
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


        private string legsPlanPTVs;
        private string bodyPlanPTVs;
       
		public event PropertyChangedEventHandler PropertyChanged;

        public string BodyPlanPTVs
        {
            get { return bodyPlanPTVs; }
            set
            {
                bodyPlanPTVs = value;
                OnPropertyChanged("BodyPTVComboBox");
            }
        }

        public string LegsPlanPTVs
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
