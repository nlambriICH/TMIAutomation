using System.Collections.Generic;

namespace TMIAutomation.ViewModel
{
    public class StructureSetSelectionViewModel : SelectionViewModel
    {
        public StructureSetSelectionViewModel(List<string> structureSetNames)
        {
            List<ItemList> structureSetSelection = new List<ItemList> { };
            foreach (string name in structureSetNames)
            {
                ItemList structureSetList = name.ToLower().Contains("iso") ?
                    new ItemList { ItemName = name, IsChecked = true }
                    : new ItemList { ItemName = name, IsChecked = false };

                structureSetSelection.Add(structureSetList);
            }
            ItemSelection = structureSetSelection;
            TextMessage = "Please select the structure sets which will be used for scheduling:";
        }
    }
}
