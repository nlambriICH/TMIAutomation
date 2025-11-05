using System;
using System.Collections.Generic;
using System.Linq;
#if !ESAPI18
using System.Text;
#endif
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Serilog;
using TMIAutomation.Async;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public class Schedule
    {
        private readonly ILogger logger = Log.ForContext<Schedule>();
        private readonly EsapiWorker esapiWorker;
        private readonly string courseId;
        private readonly string upperPlanId;
        private readonly string lowerPlanId;
        private readonly string scheduleCourseId;
        private readonly bool isocentersOnArms;
        private readonly List<string> scheduleSSStudySeriesId;
        private static readonly string SCHEDULE_PLAN_NAME = "TMLI_ISO";
        private enum SetupBeamType
        {
            CBCT,
            DRR
        }

        public Schedule(EsapiWorker esapiWorker,
                        string courseId,
                        string upperPlanId,
                        string lowerPlanId,
                        string scheduleCourseId,
                        bool isocentersOnArms,
                        List<string> scheduleSSStudySeriesId)
        {
            this.esapiWorker = esapiWorker;
            this.courseId = courseId;
            this.upperPlanId = upperPlanId;
            this.lowerPlanId = lowerPlanId;
            this.scheduleCourseId = scheduleCourseId;
            this.isocentersOnArms = isocentersOnArms;
            this.scheduleSSStudySeriesId = scheduleSSStudySeriesId;
        }

        public Task ComputeAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("Schedule context: {@context}",
                                   new List<string> { this.courseId, this.upperPlanId, this.lowerPlanId, this.scheduleCourseId, this.isocentersOnArms.ToString(), string.Join(";", this.scheduleSSStudySeriesId) });
                Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.courseId);
                ExternalPlanSetup upperPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.upperPlanId);
                ExternalPlanSetup lowerPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.lowerPlanId);
                Course scheduleCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.scheduleCourseId);
                IEnumerable<StructureSet> scheduleSS = scriptContext.Patient.StructureSets.Where(ss => IsMatchingStructureSet(ss)).OrderBy(ss => ss.Id);

                message.Report("Generating schedule plans upper-body...");
#if ESAPI18
                AddSchedulePlansV18(upperPlan, scheduleCourse, scheduleSS.Where(ss => ss.Image.ImagingOrientation == upperPlan.StructureSet.Image.ImagingOrientation));
#else
                AddSchedulePlans(upperPlan, scheduleCourse, scheduleSS.Where(ss => ss.Image.ImagingOrientation == upperPlan.StructureSet.Image.ImagingOrientation));
#endif
                progress.Report(0.2);
                CalculateDoseSchedulePlans(upperPlan, scheduleCourse, progress, message);

                message.Report("Generating schedule plans lower-extremities...");
#if ESAPI18
                AddSchedulePlansV18(lowerPlan, scheduleCourse, scheduleSS.Where(ss => ss.Image.ImagingOrientation == lowerPlan.StructureSet.Image.ImagingOrientation));
#else
                AddSchedulePlans(lowerPlan, scheduleCourse, scheduleSS.Where(ss => ss.Image.ImagingOrientation == lowerPlan.StructureSet.Image.ImagingOrientation));
#endif
                progress.Report(0.2);
                CalculateDoseSchedulePlans(lowerPlan, scheduleCourse, progress, message);
            });
        }

        private bool IsMatchingStructureSet(StructureSet ss)
        {
            bool match = false;
            foreach (string fullId in scheduleSSStudySeriesId)
            {
                string[] parts = fullId.Split('\t');

                if (parts.Length != 2)
                {
                    logger.Error("Input string {fullId} is not in the expected format: StructureSetId\\tStudyId / SeriesId.", fullId);
                    continue;
                }

                string[] subParts = parts[1].Split('/');

                if (subParts.Length != 2)
                {
                    logger.Error("Input string {fullId} is not in the expected format: StructureSetId\\tStudyId / SeriesId.", fullId);
                    continue;
                }

                string structureSetId = parts[0];
                string studyId = subParts[0].Trim();
                string seriesId = subParts[1].Trim();

                match |= ss.Id == structureSetId && ss.Image.Series.Study.Id == studyId && ss.Image.Series.Id == seriesId;
            }

            return match;
        }

#if ESAPI18
        private void AddSchedulePlansV18(PlanSetup sourcePlan, Course newCourse, IEnumerable<StructureSet> scheduleSS)
        {
            /* VMAT beams are copied to the new plan
            * Implementation works only with ESAPI V18,
            * where the gantry angle can be copied for each CP
            */

            int isoGroupCopy = 0;
            // Reorder beams: isocenters on arms after the first three isocenters groups
            List<Beam> sourcePlanBeams = sourcePlan.Beams.Where(b => Math.Abs(b.IsocenterPosition.x) <= 100)
                                                   .OrderByDescending(b => b.IsocenterPosition.z)
                                                   .ToList();
            if (this.isocentersOnArms && sourcePlan.StructureSet.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine)
            {
                sourcePlanBeams.InsertRange(6, sourcePlan.Beams.Where(b => Math.Abs(b.IsocenterPosition.x) > 100));
            }

            foreach (StructureSet ss in scheduleSS)
            {
                string isoNumber = Regex.Match(ss.Id, @"\d").Value;
                ReferencePoint refPoint = null;
                try
                {
                    // In a plug-in script, it is not possible to add a reference point for the plan not currently opened.
                    refPoint = sourcePlan.AddReferencePoint(true, null, $"PTV_ISO_{isoNumber}");
                }
                catch (ApplicationException exc)
                {
                    refPoint = sourcePlan.ReferencePoints.FirstOrDefault();
                    logger.Warning("Cannot create reference point for plan {planId}. Assingning existing reference point {refPointId}",
                                   sourcePlan.Id,
                                   refPoint.Id,
                                   exc);
                }
                Structure targetStructure = ss.Structures.FirstOrDefault(s => s.Id == sourcePlan.TargetVolumeID)
                                            ?? ss.Structures.OrderByDescending(s => s.Volume).FirstOrDefault(s => s.IsTarget);

                ExternalPlanSetup schedulePlan = newCourse.AddExternalPlanSetup(ss, targetStructure, refPoint);
                schedulePlan.Id = $"{SCHEDULE_PLAN_NAME}_{isoNumber}";
                logger.Information("Created new plan {newPlanId} into course {newCourse} using structure set {ssId}.",
                                   schedulePlan.Id,
                                   ss.Id,
                                   newCourse.Id);

                foreach (Beam beam in sourcePlanBeams)
                {
                    int beamIndex = sourcePlanBeams.IndexOf(beam);
                    if (beamIndex == isoGroupCopy || beamIndex == isoGroupCopy + 1)
                    {
                        if (!schedulePlan.Beams.Any(b => b.IsSetupField))
                        {
                            Beam refBeam = sourcePlanBeams.ElementAtOrDefault(isoGroupCopy + 1) ?? sourcePlanBeams.ElementAt(isoGroupCopy);  // if only one field in group
                            AddSetupBeamToSchedulePlan(schedulePlan, refBeam, SetupBeamType.CBCT);  // copying params for last field in group like Eclipse
                        }
                        schedulePlan.CopyBeam(beam);
                    }
                }

                if (this.isocentersOnArms &&
                    sourcePlan.StructureSet.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine &&
                    isoGroupCopy == 4)
                {
                    foreach (Beam beamArm in sourcePlan.Beams.Where(b => Math.Abs(b.IsocenterPosition.x) > 100))
                    {
                        bool isoLeftArm = beamArm.IsocenterPosition.x > 100;
                        isoNumber = isoLeftArm ? "4" : "5";
                        string ending = isoLeftArm ? "SX" : "DX";

                        schedulePlan = newCourse.AddExternalPlanSetup(ss, targetStructure, refPoint);
                        schedulePlan.Id = $"{SCHEDULE_PLAN_NAME}_{isoNumber}_{ending}";
                        logger.Information("Created new plan {newPlanId} into course {newCourse} using structure set {ssId}.",
                                           schedulePlan.Id,
                                           ss.Id,
                                           newCourse.Id);
                        AddSetupBeamToSchedulePlan(schedulePlan, beamArm, SetupBeamType.DRR);
                        schedulePlan.CopyBeam(beamArm);
                    }
                    isoGroupCopy += 2; // skip isocenters on the arms in the next iteration
                }
                isoGroupCopy += 2;
            }
        }
#else
        private void AddSchedulePlans(PlanSetup sourcePlan, Course newCourse, IEnumerable<StructureSet> scheduleSS)
        {
            int isoGroupKeep = 0;
            foreach (StructureSet ss in scheduleSS)
            {
                StringBuilder outputDiagnostics = new StringBuilder();
                ExternalPlanSetup schedulePlan = newCourse.CopyPlanSetup(sourcePlan, ss, outputDiagnostics) as ExternalPlanSetup;
                string isoNumber = Regex.Match(ss.Id, @"\d").Value;
                schedulePlan.Id = $"{SCHEDULE_PLAN_NAME}_{isoNumber}";
                logger.Information("Copied source plan {sourcePlanId} using structure set {ssId} into course {newCourse}. "
                                + "New plan {newPlanId}. "
                                + "Output diagnostics: {diagnostics}",
                                sourcePlan.Id,
                                ss.Id,
                                newCourse.Id,
                                schedulePlan.Id,
                                outputDiagnostics);

                // Reorder beams: isocenters on arms after the first three isocenters groups
                List<Beam> schedulePlanBeams = schedulePlan.Beams.Where(b => Math.Abs(b.IsocenterPosition.x) <= 100)
                                                       .OrderByDescending(b => b.IsocenterPosition.z)
                                                       .ToList();
                if (this.isocentersOnArms && sourcePlan.StructureSet.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine)
                {
                    schedulePlanBeams.InsertRange(6, schedulePlan.Beams.Where(b => Math.Abs(b.IsocenterPosition.x) > 100));
                }

                for (int i = 0; i < schedulePlanBeams.Count; i += 2)
                {
                    if (i != isoGroupKeep)
                    {
                        logger.Information("Remove beam {beam}", schedulePlanBeams[i].Id);
                        schedulePlan.RemoveBeam(schedulePlanBeams[i]);

                        /* Possible to have only one field in isocenter group
                         * (e.g., one field only for the feet)
                         */
                        Beam nextBeam = schedulePlanBeams.ElementAtOrDefault(i + 1);
                        if (nextBeam != null)
                        {
                            logger.Information("Remove beam {beam}", nextBeam.Id);
                            schedulePlan.RemoveBeam(nextBeam);
                        }
                    }
#if ESAPI16
                    else
                    {
                        // Copying params for last field in group like Eclipse
                        Beam lastBeam = schedulePlanBeams.ElementAtOrDefault(i + 1) ?? schedulePlanBeams.ElementAt(i);  // If only one field in isocenter group
                        AddSetupBeamToSchedulePlan(schedulePlan, lastBeam, SetupBeamType.CBCT);
                    }
#endif
                }

                // Add isocenters on the arms
                if (this.isocentersOnArms &&
                    sourcePlan.StructureSet.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine &&
                    isoGroupKeep == 4)
                {
                    foreach (Beam beamArm in sourcePlan.Beams.Where(b => Math.Abs(b.IsocenterPosition.x) > 100))
                    {
                        bool isoLeftArm = beamArm.IsocenterPosition.x > 100;
                        isoNumber = isoLeftArm ? "4" : "5";
                        string ending = isoLeftArm ? "SX" : "DX";

                        outputDiagnostics.Clear();
                        schedulePlan = newCourse.CopyPlanSetup(sourcePlan, ss, outputDiagnostics) as ExternalPlanSetup;
                        logger.Information("Copied source plan {sourcePlanId} using structure set {ssId} into course {newCourse}. "
                                        + "New plan {newPlanId}. "
                                        + "Output diagnostics: {diagnostics}",
                                        sourcePlan.Id,
                                        ss.Id,
                                        newCourse.Id,
                                        schedulePlan.Id,
                                        outputDiagnostics);
                        schedulePlan.Id = $"{SCHEDULE_PLAN_NAME}_{isoNumber}_{ending}";

                        // Need list to modify collection
                        List<Beam> beamsExceptArm = schedulePlan.Beams.Where(b => b.Id != beamArm.Id).ToList();
                        foreach (Beam beam in beamsExceptArm)
                        {
                            logger.Information("Remove beam {beam}", beam.Id);
                            schedulePlan.RemoveBeam(beam);
                        }
#if ESAPI16
                        AddSetupBeamToSchedulePlan(schedulePlan, schedulePlan.Beams.FirstOrDefault(), SetupBeamType.DRR);
#endif
                    }
                    isoGroupKeep += 2; // skip isocenters on the arms in the next iteration
                }
                isoGroupKeep += 2;
            }
        }
#endif

#if ESAPI16 || ESAPI18
        private void AddSetupBeamToSchedulePlan(ExternalPlanSetup schedulePlan, Beam referenceBeam, SetupBeamType type)
        {
            // ESAPI v15 allows only to modify setup fields
            ExternalBeamMachineParameters beamMachineParams = new ExternalBeamMachineParameters(referenceBeam.TreatmentUnit.Id, "6X", 600, "STATIC", "");
            if (type == SetupBeamType.DRR)
            {
                Beam drr = schedulePlan.AddSetupBeam(beamMachineParams,
                                                     jawPositions: GetMaximumAperture(referenceBeam.ControlPoints),
                                                     collimatorAngle: 0,
                                                     referenceBeam.ControlPoints.First().GantryAngle,
                                                     patientSupportAngle: 0,
                                                     referenceBeam.IsocenterPosition);
                drr.Id = nameof(SetupBeamType.DRR);
                DRRCalculationParameters drrParams = new DRRCalculationParameters(500, 1.0, 100, 1000, -1000, 1000); // bones parameters
                drr.CreateOrReplaceDRR(drrParams);
            }
            else if (type == SetupBeamType.CBCT)
            {
                Beam cbct = schedulePlan.AddSetupBeam(beamMachineParams,
                                                      jawPositions: GetMaximumAperture(referenceBeam.ControlPoints),
                                                      collimatorAngle: 0,
                                                      gantryAngle: 0,
                                                      patientSupportAngle: 0,
                                                      referenceBeam.IsocenterPosition);
                cbct.Id = nameof(SetupBeamType.CBCT);
            }
            else
            {
                logger.Warning("Cannot add setup beam for SetupBeamType {type}.", type);
            }
        }

        private static VRect<double> GetMaximumAperture(IEnumerable<ControlPoint> controlPoints)
        {
            double maxX1 = controlPoints.Min(cp => cp.JawPositions.X1);
            double maxX2 = controlPoints.Max(cp => cp.JawPositions.X2);
            double maxY1 = controlPoints.Min(cp => cp.JawPositions.Y1);
            double maxY2 = controlPoints.Max(cp => cp.JawPositions.Y2);

            return new VRect<double>(maxX1, maxY1, maxX2, maxY2);
        }
#endif

        private void CalculateDoseSchedulePlans(PlanSetup sourcePlan,
                                                Course newCourse,
                                                IProgress<double> progress,
                                                IProgress<string> message)
        {
            List<ExternalPlanSetup> schedulePlans = newCourse.ExternalPlanSetups.Where(ps => ps.TreatmentOrientation == sourcePlan.TreatmentOrientation).ToList();
            foreach (ExternalPlanSetup schedulePlan in schedulePlans)
            {
                progress.Report(0.2 / schedulePlans.Count);
                string planType = schedulePlan.StructureSet.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine ? "upper" : "lower";
                message.Report($"Calculating dose of {planType} plan {schedulePlan.Id}. Progress: {schedulePlans.IndexOf(schedulePlan) + 1}/{schedulePlans.Count}");
                schedulePlan.SetupOptimization();
                schedulePlan.CalculatePlanDose();
                schedulePlan.PlanNormalizationValue = sourcePlan.PlanNormalizationValue;
            }
        }
    }
}