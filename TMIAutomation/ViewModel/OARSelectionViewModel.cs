using System.Collections.Generic;
using System.Linq;
using FuzzySharp;

namespace TMIAutomation.ViewModel
{
    public class OARSelectionViewModel : SelectionViewModel
    {
        private static readonly int similarityThreshold = 80;
        private static readonly List<string> targetOARNames = ConfigOARNames.OarNames;

        public OARSelectionViewModel(List<string> structureNames)
        {
            List<ItemList> structureSelection = new List<ItemList> { };
            foreach(string name in structureNames)
            {
                ItemList structuresList = IsSimilarWord(name) ?
                    new ItemList { ItemName = name, IsChecked = true }
                    : new ItemList { ItemName = name, IsChecked = false };

                structureSelection.Add(structuresList);
            }
            ItemSelection = structureSelection;
            TextMessage = "Please select the OAR names for: brain, lung (left and right), liver, bowel/intestine, and bladder." +
                "\n\nThese OARs will be used to place the isocenters:";
        }

        private bool IsSimilarWord(string name)
        {
            return targetOARNames.Any(target => Fuzz.Ratio(name.ToLower(), target) >= similarityThreshold);
        }
    }
}
