using System;
using System.Windows.Input;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.CommandWpf;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation.ViewModel
{
    public class StructureOpViewModel : ViewModelBase
    {

        public Operation Operation;

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
        public string TooltipStructId
        {
            get => tooltipMessage;
            set => Set(ref tooltipMessage, value);
        }

        private string tooltipRemove;
        public string TooltipRemove
        {
            get => tooltipRemove;
            set => Set(ref tooltipRemove, value);
        }

        private string tooltipRename;
        public string TooltipRename
        {
            get => tooltipRename;
            set => Set(ref tooltipRename, value);
        }

        private readonly Structure oldStructure;
        private readonly string originalId;

        public StructureOpViewModel(Structure oldStructure, string newId, string message)
        {
            this.oldStructure = oldStructure;
            this.originalId = oldStructure.Id;
            StructureId = newId;
            TooltipRemove = $"Remove Structure {oldStructure.Id}";
            this.textMessage = $"Could not create a new Structure with Id \"{newId}\" " +
                $"because of the following reason: {message}\n\n" +
                $"Remove or Rename the existing Structure:";
        }

        public ICommand AssignStructureIdCommand => new RelayCommand(AssignStructureId);

        private void AssignStructureId()
        {
            try
            {
                // For some reason assigning the same original Id is allowed, so we check if the structure Id has not changed
                if (this.oldStructure.Id == this.StructureId)
                {
                    throw new InvalidOperationException("Cannot assign the same ID as the original structure");
                }

                // Need esapi worker to work directly on the Structure
                this.oldStructure.Id = this.structureId;
                TooltipRename = $"Rename Structure {this.oldStructure.Id} to {this.StructureId}";

                // Revert to the original Id, otherwise it could be possible to re-assign the original Id
                this.oldStructure.Id = this.originalId;
                IsValidId = true;
            }
            catch (Exception e)
            {
                IsValidId = false;
                TooltipStructId = e.Message;
            }
        }
    }

    public enum Operation
    {
        Rename,
        Remove
    }
}