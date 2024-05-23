using System;
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
        private readonly ILogger logger = Log.ForContext<Displacements>();
        private readonly EsapiWorker esapiWorker;
        private readonly string courseId;
        private readonly string upperPlanId;
        private readonly string lowerPlanId;
        private readonly bool isocentersOnArms;
        private readonly List<string> scheduleSSStudySeriesId;
        private static readonly string SCHEDULE_PLAN_NAME = "TMLI_ISO";

        public Schedule(EsapiWorker esapiWorker,
                        string courseId,
                        string upperPlanId,
                        string lowerPlanId,
                        bool isocentersOnArms,
                        List<string> scheduleSSStudySeriesId)
        {
            this.esapiWorker = esapiWorker;
            this.courseId = courseId;
            this.upperPlanId = upperPlanId;
            this.lowerPlanId = lowerPlanId;
            this.isocentersOnArms = isocentersOnArms;
            this.scheduleSSStudySeriesId = scheduleSSStudySeriesId;
        }

        public Task ComputeAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("Schedule context: {@context}",
                                   new List<string> { this.courseId, this.upperPlanId, this.lowerPlanId, this.isocentersOnArms.ToString(), string.Join(";", this.scheduleSSStudySeriesId) });
                Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.courseId);
                ExternalPlanSetup upperPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.upperPlanId);
                ExternalPlanSetup lowerPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.lowerPlanId);
                List<StructureSet> scheduleSS = scriptContext.Patient.StructureSets.Where(ss => IsMatchingStructure(ss)).OrderBy(ss => ss.Id).ToList();

                Course newCourse = scriptContext.Patient.AddCourse();
                newCourse.Id = "CScheduleAuto";

                message.Report("Generating schedule plans...");
                AddSchedulePlans(upperPlan, newCourse, scheduleSS.Where(ss => ss.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine));
                progress.Report(0.4);
                AddSchedulePlans(lowerPlan, newCourse, scheduleSS.Where(ss => ss.Image.ImagingOrientation == PatientOrientation.FeetFirstSupine));
                progress.Report(0.4);
            });
        }

        private bool IsMatchingStructure(StructureSet ss)
        {
            bool match = false;
            foreach (string fullId in scheduleSSStudySeriesId)
            {
                string[] parts = fullId.Split('\t');

                if (parts.Length != 2)
                {
                    logger.Error("Input string {fullId} is not in the expected format: StructureId\\tStudyId / SeriesId.", fullId);
                    continue;
                }

                string[] subParts = parts[1].Split('/');

                if (subParts.Length != 2)
                {
                    logger.Error("Input string {fullId} is not in the expected format: StructureId\\tStudyId / SeriesId.", fullId);
                    continue;
                }

                string structureId = parts[0];
                string studyId = subParts[0].Trim();
                string seriesId = subParts[1].Trim();

                match |= ss.Id == structureId && ss.Image.Series.Study.Id == studyId && ss.Image.Series.Id == seriesId;
            }

            return match;
        }

        private void AddSchedulePlans(PlanSetup sourcePlan, Course newCourse, IEnumerable<StructureSet> scheduleSS)
        {
            int isoGroupKeep = 0;
            foreach (StructureSet ss in scheduleSS)
            {
                StringBuilder outputDiagnostics = new StringBuilder();
                ExternalPlanSetup newPlan = newCourse.CopyPlanSetup(sourcePlan, ss, outputDiagnostics) as ExternalPlanSetup;
                string isoNumber = Regex.Match(ss.Id, @"\d").Value;
                newPlan.Id = $"{SCHEDULE_PLAN_NAME}_{isoNumber}";
                logger.Information("Copied source plan {sourcePlanId} using structure set {ssId} into course {newCourse}. "
                                + "New plan {newPlanId}. "
                                + "Output diagnostics: {diagnostics}",
                                sourcePlan.Id,
                                ss.Id,
                                newCourse.Id,
                                newPlan.Id,
                                outputDiagnostics);

                // Reorder beams: isocenters on arms after the first two isocenters groups
                List<Beam> newPlanBeams = newPlan.Beams.Where(b => Math.Abs(b.IsocenterPosition.x) <= 100)
                                                       .OrderByDescending(b => b.IsocenterPosition.z)
                                                       .ToList();
                newPlanBeams.InsertRange(6, newPlan.Beams.Where(b => Math.Abs(b.IsocenterPosition.x) > 100));
                for (int i = 0; i < newPlanBeams.Count(); i += 2)
                {
                    if (i != isoGroupKeep)
                    {
                        logger.Information("Remove beam {beam}", newPlanBeams[i].Id);
                        newPlan.RemoveBeam(newPlanBeams[i]);
                        logger.Information("Remove beam {beam}", newPlanBeams[i + 1].Id);
                        newPlan.RemoveBeam(newPlanBeams[i + 1]);
                    }
                    else
                    {
#if ESAPI16
                        /*
                         * ESAPI v15 allows only to modify setup fields
                         * Copying params for last field in group like Eclipse
                         */
                        ExternalBeamMachineParameters beamMachineParams = new ExternalBeamMachineParameters(newPlanBeams[i + 1].TreatmentUnit.Id, "6X", 600, "STATIC", "");
                        Beam cbct = newPlan.AddSetupBeam(beamMachineParams,
                                                         GetMaximumAperture(newPlanBeams[i + 1].ControlPoints), 0, 0, 0,
                                                         newPlanBeams[i + 1].IsocenterPosition);
                        cbct.Id = "CBCT";
#endif
                    }
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
                        newPlan = newCourse.CopyPlanSetup(sourcePlan, ss, outputDiagnostics) as ExternalPlanSetup;
                        logger.Information("Copied source plan {sourcePlanId} using structure set {ssId} into course {newCourse}. "
                                        + "New plan {newPlanId}. "
                                        + "Output diagnostics: {diagnostics}",
                                        sourcePlan.Id,
                                        ss.Id,
                                        newCourse.Id,
                                        newPlan.Id,
                                        outputDiagnostics);
                        newPlan.Id = $"{SCHEDULE_PLAN_NAME}_{isoNumber}_{ending}";

                        List<Beam> beamsExceptArm = newPlan.Beams.Where(b => b.Id != beamArm.Id).ToList();
                        foreach (Beam beam in beamsExceptArm)
                        {
                            logger.Information("Remove beam {beam}", beam.Id);
                            newPlan.RemoveBeam(beam);
                        }
#if ESAPI16
                        /*
                         * ESAPI v15 allows only to modify setup fields
                         * Copying params for last field in group like Eclipse
                         */
                        Beam armBeam = newPlan.Beams.FirstOrDefault();
                        ExternalBeamMachineParameters beamMachineParams = new ExternalBeamMachineParameters(armBeam.TreatmentUnit.Id, "6X", 600, "STATIC", "");
                        Beam drr = newPlan.AddSetupBeam(beamMachineParams,
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

        private static VRect<double> GetMaximumAperture(IEnumerable<ControlPoint> controlPoints)
        {
            double maxX1 = controlPoints.Min(cp => cp.JawPositions.X1);
            double maxX2 = controlPoints.Max(cp => cp.JawPositions.X2);
            double maxY1 = controlPoints.Min(cp => cp.JawPositions.Y1);
            double maxY2 = controlPoints.Max(cp => cp.JawPositions.Y2);

            return new VRect<double>(maxX1, maxY1, maxX2, maxY2);
        }
    }
}