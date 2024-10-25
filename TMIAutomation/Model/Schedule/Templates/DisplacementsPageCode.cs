using System;
using System.Collections.Generic;
using System.Linq;
using TMIAutomation.Language;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation.Model.Schedule.Templates
{
    public partial class DisplacementsPage
    {
        private readonly Patient patient;
        private readonly DateTime treatmentDate;
        private readonly bool isocentersOnArms;
        private readonly List<VVector> upperIsocenters = new List<VVector> { };
        private readonly List<string> scheduleUpperPlanName = new List<string> { };
        private static readonly List<string> upperIsoLocation = new List<string> { Resources.Head, Resources.Shoulders, Resources.Thorax, Resources.Abdomen, Resources.Pelvis };
        private readonly string upperMarkersLocation = Resources.DefaultUpperMarkerLocationMsg;

        private readonly List<VVector> lowerIsocenters = new List<VVector> { };
        private readonly List<string> scheduleLowerPlanName = new List<string> { };
        private readonly string lowerMarkersLocation = Resources.DefaultLowerMarkerLocationMsg;

        public DisplacementsPage(ExternalPlanSetup upperPlan,
                                 ExternalPlanSetup lowerPlan,
                                 IEnumerable<PlanSetup> schedulePlans,
                                 DateTime treatmentDate,
                                 bool isocentersOnArms)
        {
            this.patient = upperPlan.Course.Patient;
            this.treatmentDate = treatmentDate;
            this.isocentersOnArms = isocentersOnArms;

            // Upper-body
            List<Beam> beams = upperPlan.Beams.OrderByDescending(b => b.IsocenterPosition.z).ToList();

            if (this.isocentersOnArms)
            {
                PrepareIsocentersWithArms(upperPlan, schedulePlans, beams);
            }
            else
            {
                PrepareIsocentersWithoutArms(upperPlan, schedulePlans, beams);
            }

            // Markers location found based on heuristic rule on distance iso - user origin
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
                UpdateSchedulePlanNames(schedulePlans, beams[i], lowerPlan.StructureSet.Image.ImagingOrientation);
            }

            // Markers location found based on heuristic rule on distance iso - user origin
            for (int i = 0; i < lowerIsocenters.Count; ++i)
            {
                if (lowerIsocenters[i].Length < 120)
                {
                    if (lowerIsocenters.Count == 3)
                    {
                        switch (i)
                        {
                            case 0:
                                this.lowerMarkersLocation = Resources.AboveKnees;
                                break;
                            case 1:
                                this.lowerMarkersLocation = Resources.OnKnees;
                                break;
                            default:
                                this.lowerMarkersLocation = Resources.BelowKnees;
                                break;
                        }
                    }
                    else
                    {
                        this.lowerMarkersLocation = i < 1 ? Resources.AboveKnees : Resources.BelowKnees;
                    }

                    break;
                }
            }
        }

        private void PrepareIsocentersWithArms(ExternalPlanSetup upperPlan, IEnumerable<PlanSetup> schedulePlans, List<Beam> beams)
        {
            var beamsAlongBody = beams.Where(b => Math.Abs(upperPlan.StructureSet.Image.DicomToUser(b.IsocenterPosition, upperPlan).x) < 150).ToList();
            Beam beamLeftArm = beams.FirstOrDefault(b => upperPlan.StructureSet.Image.DicomToUser(b.IsocenterPosition, upperPlan).x > 150);
            Beam beamRightArm = beams.FirstOrDefault(b => upperPlan.StructureSet.Image.DicomToUser(b.IsocenterPosition, upperPlan).x < -150);

            for (int i = 0; i < beamsAlongBody.Count; i += 2)
            {
                this.upperIsocenters.Add(upperPlan.StructureSet.Image.DicomToUser(beamsAlongBody[i].IsocenterPosition, upperPlan));
                UpdateSchedulePlanNames(schedulePlans, beamsAlongBody[i], upperPlan.StructureSet.Image.ImagingOrientation);
                if (i == 4)
                {
                    this.upperIsocenters.Add(upperPlan.StructureSet.Image.DicomToUser(beamLeftArm.IsocenterPosition, upperPlan));
                    UpdateSchedulePlanNames(schedulePlans, beamLeftArm, upperPlan.StructureSet.Image.ImagingOrientation);
                    // Append string to specify iso refers to left arm
                    scheduleUpperPlanName[scheduleUpperPlanName.Count - 1] = string.Join(" ", scheduleUpperPlanName.Last(), "–", Resources.IsoOnLeftArm);

                    this.upperIsocenters.Add(upperPlan.StructureSet.Image.DicomToUser(beamRightArm.IsocenterPosition, upperPlan));
                    UpdateSchedulePlanNames(schedulePlans, beamRightArm, upperPlan.StructureSet.Image.ImagingOrientation);
                    // Append string to specify iso refers to right arm
                    scheduleUpperPlanName[scheduleUpperPlanName.Count - 1] = string.Join(" ", scheduleUpperPlanName.Last(), "–", Resources.IsoOnRightArm);
                }
            }
        }

        private void PrepareIsocentersWithoutArms(ExternalPlanSetup upperPlan, IEnumerable<PlanSetup> schedulePlans, List<Beam> beams)
        {
            for (int i = 0; i < beams.Count; i += 2)
            {
                this.upperIsocenters.Add(upperPlan.StructureSet.Image.DicomToUser(beams[i].IsocenterPosition, upperPlan));
                UpdateSchedulePlanNames(schedulePlans, beams[i], upperPlan.StructureSet.Image.ImagingOrientation);
            }
        }

        private void UpdateSchedulePlanNames(IEnumerable<PlanSetup> schedulePlans, Beam currentBeam, PatientOrientation orientation)
        {
            foreach (var schedulePlan in schedulePlans)
            {
                foreach (var beam in schedulePlan.Beams.Where(b => !b.IsSetupField))
                {
                    if (VVector.Distance(beam.IsocenterPosition, currentBeam.IsocenterPosition) < 0.01)
                    {
                        if (orientation == PatientOrientation.HeadFirstSupine)
                        {
                            scheduleUpperPlanName.Add(schedulePlan.Id);
                        }
                        else if (orientation == PatientOrientation.FeetFirstSupine)
                        {
                            scheduleLowerPlanName.Add(schedulePlan.Id);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Scheduling supported only for HFS or FFS orientation. Orientation was {orientation}");
                        }
                    }

                    break;
                }
            }
        }
    }
}