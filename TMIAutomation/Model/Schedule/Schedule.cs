using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

                AddSchedulePlan(upperPlan, newCourse, scheduleSS.Where(ss => ss.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine));
                AddSchedulePlan(lowerPlan, newCourse, scheduleSS.Where(ss => ss.Image.ImagingOrientation == PatientOrientation.FeetFirstSupine));
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
                    Log.Error("Input string {fullId} is not in the expected format: StructureId\\tStudyId / SeriesId.", fullId);
                    continue;
                }

                string[] subParts = parts[1].Split(new string[] { "/" }, StringSplitOptions.None);

                if (subParts.Length != 2)
                {
                    Log.Error("Input string {fullId} is not in the expected format: StructureId\\tStudyId / SeriesId.", fullId);
                    continue;
                }

                string structureId = parts[0];
                string studyId = subParts[0].Trim();
                string seriesId = subParts[1].Trim();

                match |= ss.Id == structureId && ss.Image.Series.Study.Id == studyId && ss.Image.Series.Id == seriesId;
            }

            return match;
        }

        private void AddSchedulePlan(PlanSetup sourcePlan, Course newCourse, IEnumerable<StructureSet> scheduleSS)
        {
            int isoGroupKeep = 0;
            foreach (StructureSet ss in scheduleSS)
            {
                StringBuilder outputDiagnostics = new StringBuilder();
                ExternalPlanSetup newPlan = newCourse.CopyPlanSetup(sourcePlan, ss, outputDiagnostics) as ExternalPlanSetup;
                Log.Information("Copied source plan {sourcePlanId} using structure set {ssId} into course {newCourse}. "
                                + "New plan {newPlanId}. "
                                + "Output diagnostics: {diagnostics}",
                                sourcePlan.Id,
                                ss.Id,
                                newCourse.Id,
                                newPlan.Id,
                                outputDiagnostics);
                newPlan.Id = $"RA_TMLI_{ss.Id}";

                List<Beam> newPlanBeams = newPlan.Beams.OrderByDescending(b => b.IsocenterPosition.z).ToList();
                for (int i = 0; i < newPlanBeams.Count(); i += 2)
                {
                    if (i != isoGroupKeep)
                    {
                        Log.Information("Remove beam {beam}", newPlanBeams[i].Id);
                        newPlan.RemoveBeam(newPlanBeams[i]);
                        Log.Information("Remove beam {beam}", newPlanBeams[i + 1].Id);
                        newPlan.RemoveBeam(newPlanBeams[i + 1]);
                    }
                }
                isoGroupKeep += 2;
            }
        }
    }
}
