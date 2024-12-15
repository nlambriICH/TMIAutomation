using System.Collections.Generic;
using GalaSoft.MvvmLight;

namespace TMIAutomation.ViewModel
{
    public class SelectionViewModel : ViewModelBase
    {
        private List<ItemList> itemSelection;
        public List<ItemList> ItemSelection
        {
            get => itemSelection;
            set => Set(ref itemSelection, value);
        }

        private string textMessage;
        public string TextMessage
        {
            get => textMessage;
            set => Set(ref textMessage, value);
        }

        public class ItemList
        {
            public string ItemName { get; set; }
            public bool IsChecked { get; set; }
        }
    }
}
