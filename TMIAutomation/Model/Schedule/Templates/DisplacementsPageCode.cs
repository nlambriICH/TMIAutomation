using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation.Model.Schedule.Templates
{
    public partial class DisplacementsPage
    {
        private readonly Patient patient;
        private readonly DateTime treatmentDate;
        private readonly List<VVector> upperIsocenters = new List<VVector> { };
        private readonly List<string> scheduleUpperPlanName = new List<string> { };
        private static readonly List<string> upperIsoLocation = new List<string> { "testa", "spalle", "torace", "addome", "pelvi" };
        private readonly string upperMarkersLocation;

        private readonly List<VVector> lowerIsocenters = new List<VVector> { };
        private readonly List<string> scheduleLowerPlanName = new List<string> { };
        private readonly string lowerMarkersLocation;


        public DisplacementsPage(ExternalPlanSetup upperPlan, ExternalPlanSetup lowerPlan, IEnumerable<PlanSetup> schedulePlans, DateTime treatmentDate)
        {
            this.patient = upperPlan.Course.Patient;
            this.treatmentDate = treatmentDate;

            // Upper-body
            List<Beam> beams = upperPlan.Beams.OrderByDescending(b => b.IsocenterPosition.z).ToList();
            for (int i = 0; i < beams.Count; i += 2)
            {
                this.upperIsocenters.Add(upperPlan.StructureSet.Image.DicomToUser(beams[i].IsocenterPosition, upperPlan));
                foreach (var schedulePlan in schedulePlans)
                {
                    foreach(var beam in schedulePlan.Beams.Where(b => !b.IsSetupField))
                    {
                        if (Math.Abs(beam.IsocenterPosition.z - beams[i].IsocenterPosition.z) < 0.01)
                        {
                            scheduleUpperPlanName.Add(schedulePlan.Id);
                        }

                        break;
                    }
                }
            }
            for (int i = 0; i < upperIsocenters.Count; ++i)
            {
                if (upperIsocenters[i].Length < 100)
                {
                    this.upperMarkersLocation = upperIsoLocation[i];

                    break;
                }
            }

            // Lower-extremities
            beams = lowerPlan.Beams.OrderByDescending(b => b.IsocenterPosition.z).ToList(); // Descending because ESAPI flips x and z wrt DICOM
            for (int i = 0; i < beams.Count; i += 2)
            {
                this.lowerIsocenters.Add(lowerPlan.StructureSet.Image.DicomToUser(beams[i].IsocenterPosition, lowerPlan));
                foreach (var schedulePlan in schedulePlans)
                {
                    foreach (var beam in schedulePlan.Beams.Where(b => !b.IsSetupField))
                    {
                        if (Math.Abs(beam.IsocenterPosition.z - beams[i].IsocenterPosition.z) < 0.01)
                        {
                            scheduleLowerPlanName.Add(schedulePlan.Id);
                        }

                        break;
                    }
                }
            }
            for (int i = 0; i < lowerIsocenters.Count; ++i)
            {
                if (lowerIsocenters[i].Length < 120)
                {
                    this.lowerMarkersLocation = i < 1 ? "sopra ginocchia" : "sotto ginocchia";

                    break;
                }
            }
        }
    }
}
