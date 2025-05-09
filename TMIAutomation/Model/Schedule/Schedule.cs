﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                AddSchedulePlans(upperPlan, scheduleCourse, scheduleSS.Where(ss => ss.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine));
                progress.Report(0.2);
                CalculateDoseSchedulePlans(upperPlan, scheduleCourse, progress, message);

                message.Report("Generating schedule plans lower-extremities...");
                AddSchedulePlans(lowerPlan, scheduleCourse, scheduleSS.Where(ss => ss.Image.ImagingOrientation == PatientOrientation.FeetFirstSupine));
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

                // Reorder beams: isocenters on arms after the first two isocenters groups
                List<Beam> schedulePlanBeams = schedulePlan.Beams.Where(b => Math.Abs(b.IsocenterPosition.x) <= 100)
                                                       .OrderByDescending(b => b.IsocenterPosition.z)
                                                       .ToList();
                if (ss.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine)
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
                        /*
                         * ESAPI v15 allows only to modify setup fields
                         * Copying params for last field in group like Eclipse
                         */
                        Beam lastBeam = schedulePlanBeams.ElementAtOrDefault(i + 1) ?? schedulePlanBeams.ElementAt(i);  // If only one field in isocenter group
                        ExternalBeamMachineParameters beamMachineParams = new ExternalBeamMachineParameters(lastBeam.TreatmentUnit.Id, "6X", 600, "STATIC", "");
                        Beam cbct = schedulePlan.AddSetupBeam(beamMachineParams,
                                                         GetMaximumAperture(lastBeam.ControlPoints), 0, 0, 0,
                                                         lastBeam.IsocenterPosition);
                        cbct.Id = "CBCT";
                    }
#endif
                }

                // Add isocenters on the arms
                if (this.isocentersOnArms &&
                    ss.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine &&
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
                        /*
                         * ESAPI v15 allows only to modify setup fields
                         * Copying params for last field in group like Eclipse
                         */
                        Beam armBeam = schedulePlan.Beams.FirstOrDefault();
                        ExternalBeamMachineParameters beamMachineParams = new ExternalBeamMachineParameters(armBeam.TreatmentUnit.Id, "6X", 600, "STATIC", "");
                        Beam drr = schedulePlan.AddSetupBeam(beamMachineParams,
                                                        GetMaximumAperture(armBeam.ControlPoints), 0,
                                                        armBeam.ControlPoints.First().GantryAngle, 0,
                                                        armBeam.IsocenterPosition);
                        drr.Id = "DRR";
                        DRRCalculationParameters drrParams = new DRRCalculationParameters(500, 1.0, 100, 1000, -1000, 1000);
                        drr.CreateOrReplaceDRR(drrParams);
#endif
                    }
                    isoGroupKeep += 2; // skip isocenters on the arms in the next iteration
                }
                isoGroupKeep += 2;
            }
        }

#if ESAPI16
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