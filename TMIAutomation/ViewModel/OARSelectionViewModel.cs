using System.Collections.Generic;
using System.Linq;
using FuzzySharp;
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

        private static readonly int similarityThreshold = 80;
        private static readonly List<string> targetOARNames = ConfigOARNames.OarNames;

        public OARSelectionViewModel(List<string> structureNames)
        {
            List<StructuresList> structureSelection = new List<StructuresList> { };
            foreach(string name in structureNames)
            {
                StructuresList structuresList = IsSimilarWord(name) ?
                    new StructuresList { StructureName = name, IsChecked = true }
                    : new StructuresList { StructureName = name, IsChecked = false };

                structureSelection.Add(structuresList);
            }
            StructureSelection = structureSelection;
            TextMessage = "Please select the OAR names for: brain, lung (left and right), liver, bowel/intestine, and bladder." +
                "\n\nThese OARs will be used to place the isocenters:";
        }

        private bool IsSimilarWord(string name)
        {
            return targetOARNames.Any(target => Fuzz.Ratio(name.ToLower(), target) >= similarityThreshold);
        }

        public class StructuresList
        {
            public string StructureName { get; set; }
            public bool IsChecked { get; set; }
        }
    }
}
