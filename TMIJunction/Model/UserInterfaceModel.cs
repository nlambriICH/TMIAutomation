using System.ComponentModel;
using VMS.TPS.Common.Model.API;

namespace TMIJunction
{
    class UserInterfaceModel : INotifyPropertyChanged
    {
        public UserInterfaceModel(Course latestCourse)
        {
            this.latestCourse = latestCourse;
        }

        private Course latestCourse;

        public Course LatestCourse
        {
            get { return latestCourse; }
            set { latestCourse = value; }
        }

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

        private string machineName;

        public string MachineName
        {
            get { return machineName; }
            set { machineName = value; }
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
