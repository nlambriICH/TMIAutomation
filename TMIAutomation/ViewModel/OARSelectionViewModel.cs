using System.Collections.Generic;
using System.Text.RegularExpressions;
using GalaSoft.MvvmLight;

namespace TMIAutomation.ViewModel
{
    public class OARSelectionViewModel : ViewModelBase
    {
        private List<StructuresList> structureSelection;
        public List<StructuresList> StructureSelection
        {
            get => structureSelection;
            set => Set(ref structureSelection, value);
        }

        private string textMessage;
        public string TextMessage
        {
            get => textMessage;
            set => Set(ref textMessage, value);
        }

        private static readonly string pattern = @"\b(?:brain|lung|liver|bowel|intestine|bladder)\b";

        public OARSelectionViewModel(List<string> structureNames)
        {
            List<StructuresList> structureSelection = new List<StructuresList> { };
            foreach(string name in structureNames)
            {
                if (Regex.Match(name, pattern, RegexOptions.IgnoreCase).Success)
                {
                    structureSelection.Add(new StructuresList { StructureName = name, IsChecked = true });
                }
                else
                {
                    structureSelection.Add(new StructuresList { StructureName = name, IsChecked = false });
                }
            }
            StructureSelection = structureSelection;
            TextMessage = "Please select the OAR names for: brain, lung (left and right), liver, bowel/intestine, and bladder." +
                "\n\nThese OARs will be used to place the isocenters:";
        }

        public class StructuresList
        {
            public string StructureName { get; set; }
            public bool IsChecked { get; set; }
        }
    }
}
