using System;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation.ViewModel
{
    public class RenameStructureViewModel : ViewModelBase
    {
        private string structureId;
        public string StructureId
        {
            get => structureId;
            set => Set(ref structureId, value);
        }

        private bool isValidId;
        public bool IsValidId
        {
            get => isValidId;
            set => Set(ref isValidId, value);
        }

        private string textMessage;
        public string TextMessage
        {
            get => textMessage;
            set => Set(ref textMessage, value);
        }

        private string tooltipMessage;
        public string TooltipMessage
        {
            get => tooltipMessage;
            set => Set(ref tooltipMessage, value);
        }

        private readonly Structure oldStructure;

        public RenameStructureViewModel(Structure oldStructure, string newId, string message)
        {
            this.oldStructure = oldStructure;
            this.textMessage = $"Could not create a new Structure with Id \"{newId}\" " +
                $"because of the following reason:\n{message}\n\n" +
                $"Please insert a Structure Id to rename the existing Structure:";
        }

        public ICommand AssignStructureIdCommand => new RelayCommand(AssignStructureId);

        private void AssignStructureId()
        {
            try
            {
                // need esapi worker to work directly on the Structure (?)
                this.oldStructure.Id = this.structureId;
                IsValidId = true;
            }
            catch (Exception e)
            {
                IsValidId = false;
                TooltipMessage = e.Message;
            }
        }
    }
}