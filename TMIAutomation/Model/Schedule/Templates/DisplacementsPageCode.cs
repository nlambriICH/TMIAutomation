using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using TMIAutomation.Language;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation.Model.Schedule.Templates
{
    public partial class DisplacementsPage
    {
        private readonly ILogger logger = Log.ForContext<DisplacementsPage>();
        private readonly ExternalPlanSetup upperPlan;
        private readonly ExternalPlanSetup lowerPlan;
        private readonly IEnumerable<PlanSetup> schedulePlans;
        private readonly DateTime treatmentDate;
        private readonly bool isocentersOnArms;

        private readonly List<VVector> upperIsocenters = new List<VVector> { };
        private readonly List<string> scheduleUpperPlanName = new List<string> { };
        private static readonly List<string> upperIsoLocation = new List<string> { Resources.Head, Resources.Shoulders, Resources.Thorax, Resources.Abdomen, Resources.Pelvis };
        private string upperMarkersLocation = Resources.DefaultUpperMarkerLocationMsg;

        private readonly List<VVector> lowerIsocenters = new List<VVector> { };
        private readonly List<string> scheduleLowerPlanName = new List<string> { };
        private string lowerMarkersLocation = Resources.DefaultLowerMarkerLocationMsg;

        public DisplacementsPage(ExternalPlanSetup upperPlan,
                                 ExternalPlanSetup lowerPlan,
                                 IEnumerable<PlanSetup> schedulePlans,
                                 DateTime treatmentDate,
                                 bool isocentersOnArms)
        {
            this.upperPlan = upperPlan;
            this.lowerPlan = lowerPlan;
            this.schedulePlans = schedulePlans;
            this.treatmentDate = treatmentDate;
            this.isocentersOnArms = isocentersOnArms;
        }

        public void PopulateTemplate()
        {
            logger.Information(
                "DisplacementsPage context: {@context}",
                new List<string> { this.upperPlan.Id, this.lowerPlan.Id, string.Join(",", this.schedulePlans.Select(ps => ps.Id)), this.treatmentDate.ToString("ddMMyyyy"), this.isocentersOnArms.ToString() });

            // Upper-body
            if (this.isocentersOnArms)
            {
                PrepareIsocentersWithArms();
            }
            else
            {
                PrepareIsocentersWithoutArms();
            }

            // Markers location found based on heuristic rule on distance iso - user origin
            int foundIndex = this.upperIsocenters.FindIndex(iso => iso.Length < 100);
            if (foundIndex != -1)
            {
                this.upperMarkersLocation = upperIsoLocation[foundIndex];
            }

            logger.Information("Populated UpperIsocenters (mm): {@upperIsocenters}", this.upperIsocenters);
            logger.Information("Populated ScheduleUpperPlanName: {@scheduleUpperPlanName}", this.scheduleUpperPlanName);
            logger.Information("Populated UpperMarkersLocation: {@upperMarkersLocation}", this.upperMarkersLocation);

            // Lower-extremities
            PrepareIsocentersLower();

            // Markers location found based on heuristic rule on distance iso - user origin
            foundIndex = this.lowerIsocenters.FindIndex(iso => iso.Length < 120);
            if (foundIndex != -1)
            {
                this.lowerMarkersLocation = lowerIsocenters.Count == 3
                    ? GetKneeLocationForThreeIsocenters(foundIndex)
                    : (foundIndex < 1 ? Resources.AboveKnees : Resources.BelowKnees);
            }

            logger.Information("Populated LowerIsocenters (mm): {@lowerIsocenters}", this.lowerIsocenters);
            logger.Information("Populated ScheduleLowerPlanName: {@scheduleLowerPlanName}", this.scheduleLowerPlanName);
            logger.Information("Populated LowerMarkersLocation: {@lowerMarkersLocation}", this.lowerMarkersLocation);
        }

        private static string GetKneeLocationForThreeIsocenters(int index)
        {
            switch (index)
            {
                case 0:
                    return Resources.AboveKnees;
                case 1:
                    return Resources.OnKnees;
                default:
                    return Resources.BelowKnees;
            }
        }

        private void PrepareIsocentersWithArms()
        {
            Image structureSetImage = this.upperPlan.StructureSet.Image;
            List<Beam> beamsAlongBody = this.upperPlan.Beams.OrderByDescending(b => b.IsocenterPosition.z)
                .Where(b => Math.Abs(structureSetImage.DicomToUser(b.IsocenterPosition, this.upperPlan).x) < 150)
                .ToList();

            for (int i = 0; i < beamsAlongBody.Count; i += 2)
            {
                this.upperIsocenters.Add(structureSetImage.DicomToUser(beamsAlongBody[i].IsocenterPosition, this.upperPlan));
                UpdateSchedulePlanNames(beamsAlongBody[i], structureSetImage.ImagingOrientation);
                if (i == 4)
                {
                    Beam beamLeftArm = this.upperPlan.Beams.FirstOrDefault(b => structureSetImage.DicomToUser(b.IsocenterPosition, this.upperPlan).x > 150);
                    this.upperIsocenters.Add(structureSetImage.DicomToUser(beamLeftArm.IsocenterPosition, this.upperPlan));
                    UpdateSchedulePlanNames(beamLeftArm, structureSetImage.ImagingOrientation);

                    // Append string to specify iso refers to left arm
                    if (this.scheduleUpperPlanName.Count > 0)
                    {
                        int lastIndex = this.scheduleUpperPlanName.Count - 1;
                        this.scheduleUpperPlanName[lastIndex] =
                            $"{this.scheduleUpperPlanName[lastIndex]} – {Resources.IsoOnLeftArm}";
                    }

                    Beam beamRightArm = this.upperPlan.Beams.FirstOrDefault(b => structureSetImage.DicomToUser(b.IsocenterPosition, this.upperPlan).x < -150);
                    this.upperIsocenters.Add(structureSetImage.DicomToUser(beamRightArm.IsocenterPosition, this.upperPlan));
                    UpdateSchedulePlanNames(beamRightArm, structureSetImage.ImagingOrientation);

                    // Append string to specify iso refers to right arm
                    if (this.scheduleUpperPlanName.Count > 0)
                    {
                        int lastIndex = this.scheduleUpperPlanName.Count - 1;
                        this.scheduleUpperPlanName[lastIndex] =
                            $"{this.scheduleUpperPlanName[lastIndex]} – {Resources.IsoOnRightArm}";
                    }
                }
            }
        }

        private void PrepareIsocentersWithoutArms()
        {
            Image structureSetImage = this.upperPlan.StructureSet.Image;
            List<Beam> beams = this.upperPlan.Beams.OrderByDescending(b => b.IsocenterPosition.z).ToList();
            for (int i = 0; i < beams.Count; i += 2)
            {
                this.upperIsocenters.Add(structureSetImage.DicomToUser(beams[i].IsocenterPosition, this.upperPlan));
                UpdateSchedulePlanNames(beams[i], structureSetImage.ImagingOrientation);
            }
        }
        private void PrepareIsocentersLower()
        {
            Image structureSetImage = this.lowerPlan.StructureSet.Image;
            // Descending because ESAPI flips x and z wrt DICOM
            List<Beam> beams = this.lowerPlan.Beams.OrderByDescending(b => b.IsocenterPosition.z).ToList();
            for (int i = 0; i < beams.Count; i += 2)
            {
                this.lowerIsocenters.Add(structureSetImage.DicomToUser(beams[i].IsocenterPosition, this.lowerPlan));
                UpdateSchedulePlanNames(beams[i], structureSetImage.ImagingOrientation);
            }
        }

        private void UpdateSchedulePlanNames(Beam currentBeam, PatientOrientation orientation)
        {
            if (orientation != PatientOrientation.HeadFirstSupine && orientation != PatientOrientation.FeetFirstSupine)
            {
                throw new InvalidOperationException(
                    $"Scheduling supported only for HFS or FFS orientation. Orientation was {orientation}");
            }

            List<string> targetList = orientation == PatientOrientation.HeadFirstSupine
                ? this.scheduleUpperPlanName
                : this.scheduleLowerPlanName;

            foreach (PlanSetup schedulePlan in this.schedulePlans)
            {
                Beam matchingBeam = schedulePlan.Beams.FirstOrDefault(b => !b.IsSetupField && VVector.Distance(b.IsocenterPosition, currentBeam.IsocenterPosition) < 0.01);

                if (matchingBeam != null)
                {
                    targetList.Add(schedulePlan.Id);
                    break;
                }
            }
        }
    }
}