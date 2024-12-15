using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIAutomation.Async;
using TMIAutomation.Language;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public class ModelBase
    {
        private readonly EsapiWorker esapiWorker;
        public enum PlanType
        {
            Up,
            Down
        }

        public ModelBase(EsapiWorker esapiWorker)
        {
            this.esapiWorker = esapiWorker;
        }

        public Task<List<string>> GetCoursesAsync(bool schedule = false)
        {
            return this.esapiWorker.RunAsync(scriptContext => GetCourses(scriptContext, schedule), isWriteable: false);
        }

        public List<string> GetCourses(PluginScriptContext scriptContext, bool schedule)
        {
            List<Course> orderedCourses;
            if (schedule)
            {
                orderedCourses = scriptContext.Patient.Courses.OrderByDescending(c => c.PlanSetups.Count(ps => IsPlanApproved(ps))).ToList();

                // If no approved plan in any course, set first course Id in list to create new course
                List<string> orderedCourseId = orderedCourses.Select(c => c.Id).ToList();
                if (!orderedCourses.First().PlanSetups.Any(ps => IsPlanApproved(ps)))
                {
                    orderedCourseId.Insert(0, Resources.NewCourseListBox);
                }
                else
                {
                    orderedCourseId.Add(Resources.NewCourseListBox);
                }

                return orderedCourseId;
            }
            else
            {
                orderedCourses = scriptContext.Patient.Courses.OrderByDescending(c => c.HistoryDateTime).ToList();
                Course courseInScope = scriptContext.Course;
                if (orderedCourses.Remove(courseInScope))
                {
                    orderedCourses.Insert(0, courseInScope);
                }

                return orderedCourses.Select(c => c.Id).ToList();
            }

            bool IsPlanApproved(PlanSetup ps)
            {
                return ps.ApprovalStatus == PlanSetupApprovalStatus.PlanningApproved
                       || ps.ApprovalStatus == PlanSetupApprovalStatus.TreatmentApproved
                       || ps.ApprovalStatus == PlanSetupApprovalStatus.ExternallyApproved;
            }
        }

        public Task<List<string>> GetPlansAsync(string courseId, PlanType planType)
        {
            return this.esapiWorker.RunAsync(scriptContext => GetPlans(scriptContext, courseId, planType), isWriteable: false);
        }

        public List<string> GetPlans(PluginScriptContext scriptContext, string courseId, PlanType planType)
        {
            Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == courseId);
            PlanSetup planInScope = scriptContext.PlanSetup;

            List<PlanSetup> orderedPlans = new List<PlanSetup>();
            switch (planType)
            {
                case PlanType.Up:
                    orderedPlans = targetCourse.PlanSetups.Where(p => p.StructureSet.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine)
                                        .OrderByDescending(p => p.CreationDateTime)
                                        .ToList();
                    break;
                case PlanType.Down:
                    orderedPlans = targetCourse.PlanSetups.Where(p => p.StructureSet.Image.ImagingOrientation == PatientOrientation.FeetFirstSupine)
                                        .OrderByDescending(p => p.CreationDateTime)
                                        .ToList();
                    break;
            }

            if (orderedPlans.Remove(planInScope))
            {
                orderedPlans.Insert(0, planInScope);
            }

            return orderedPlans.Select(ps => ps.Id).ToList();
        }

        public Task<bool> CheckIsocentersOnArmsAsync(string courseId, string planId)
        {
            return this.esapiWorker.RunAsync(scriptContext => CheckIsocentersOnArms(scriptContext, courseId, planId), isWriteable: false);
        }

        public bool CheckIsocentersOnArms(PluginScriptContext scriptContext, string courseId, string planId)
        {
            Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == courseId);
            PlanSetup selectedPlan = targetCourse.PlanSetups.FirstOrDefault(ps => ps.Id == planId);

            return selectedPlan.Beams.Count(b => !b.IsSetupField && Math.Abs(selectedPlan.StructureSet.Image.DicomToUser(b.IsocenterPosition, selectedPlan).x) > 10) == 2;
        }

        public Task<List<string>> GetPTVsFromPlanAsync(string courseId, string planId)
        {
            return this.esapiWorker.RunAsync(scriptContext => GetPTVsFromPlan(scriptContext, courseId, planId), isWriteable: false);
        }

        public List<string> GetPTVsFromPlan(PluginScriptContext scriptContext, string courseId, string planId)
        {
            Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == courseId);
            PlanSetup selectedPlan = targetCourse.PlanSetups.FirstOrDefault(ps => ps.Id == planId);
            StructureSet targetStructureSet = selectedPlan == null ? scriptContext.StructureSet : selectedPlan.StructureSet;

            return targetStructureSet == null ? new List<string>() : targetStructureSet.Structures.Where(s => s.DicomType == "PTV")
                                                                                                  .OrderByDescending(s => s.Volume)
                                                                                                  .Select(s => s.Id)
                                                                                                  .ToList();
        }

        public Task<List<string>> GetOARNamesAsync(string courseId, string planId)
        {
            return this.esapiWorker.RunAsync(scriptContext => GetOARNames(scriptContext, courseId, planId), isWriteable: false);
        }

        public List<string> GetOARNames(PluginScriptContext scriptContext, string courseId, string planId)
        {
            Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == courseId);
            PlanSetup selectedPlan = targetCourse.PlanSetups.FirstOrDefault(ps => ps.Id == planId);
            return selectedPlan.StructureSet.Structures.Where(s => s.DicomType == "ORGAN" || s.DicomType == "AVOIDANCE")
                .OrderBy(s => s.Id)
                .Select(s => s.Id)
                .ToList();
        }

        public Task<List<string>> GetSSStudySeriesIdAsync()
        {
            return this.esapiWorker.RunAsync(scriptContext => GetSSStudySeriesId(scriptContext), isWriteable: false);
        }

        public List<string> GetSSStudySeriesId(PluginScriptContext scriptContext)
        {
            return scriptContext.Patient.StructureSets.OrderBy(s => s.Structures.Count())
                .ThenBy(s => s.Id)
                .Select(s => string.Concat(s.Id, "\t", s.Image.Series.Study.Id, " / ", s.Image.Series.Id))
                .ToList();
        }

        public Task<List<string>> GetPTVsFromImgOrientationAsync(PatientOrientation patientOrientation)
        {
            return this.esapiWorker.RunAsync(scriptContext => GetPTVsFromImgOrientation(scriptContext, patientOrientation), isWriteable: false);
        }

        public List<string> GetPTVsFromImgOrientation(PluginScriptContext scriptContext, PatientOrientation patientOrientation)
        {
            StructureSet targetSS = this.GetTargetStructureSet(scriptContext, patientOrientation);
            return targetSS == null ? new List<string>()
            : targetSS.Structures.Where(s => s.DicomType == "PTV")
                        .OrderByDescending(s => s.Volume)
                        .Select(s => s.Id)
                        .ToList();
        }

        private StructureSet GetTargetStructureSet(PluginScriptContext scriptContext, PatientOrientation patientOrientation)
        {
            return scriptContext.StructureSet != null && scriptContext.StructureSet.Image.ImagingOrientation == patientOrientation
                ? scriptContext.StructureSet
                : scriptContext.Patient.StructureSets.Where(ss => ss.Image.ImagingOrientation == patientOrientation)
                                              .OrderByDescending(ss => ss.HistoryDateTime)
                                              .FirstOrDefault();
        }

        public Task<List<string>> GetRegistrationsAsync()
        {
            return this.esapiWorker.RunAsync(scriptContext => GetRegistrations(scriptContext), isWriteable: false);
        }

        public List<string> GetRegistrations(PluginScriptContext scriptContext)
        {
            return scriptContext.Patient.Registrations.OrderByDescending(reg => reg.CreationDateTime)
                                                        .Select(reg => reg.Id)
                                                        .ToList();
        }

        public Task GenerateUpperJunctionAsync(string courseId, string upperPlanId, string upperPTVId, IProgress<double> progress, IProgress<string> message)
        {
            UpperJunction upperJunction = new UpperJunction(this.esapiWorker, courseId, upperPlanId, upperPTVId);
            return upperJunction.CreateAsync(progress, message);
        }

        public Task GenerateUpperControlAsync(string courseId, string upperPlanId, string upperPTVId, IProgress<double> progress, IProgress<string> message)
        {
            UpperControl upperControl = new UpperControl(this.esapiWorker, courseId, upperPlanId, upperPTVId);
            return upperControl.CreateAsync(progress, message);
        }

        public Task SetInContextOrCreateAutoPlanAsync(string courseId, PlanType planType)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                PatientOrientation patientOrientation = planType == PlanType.Up ? PatientOrientation.HeadFirstSupine : PatientOrientation.FeetFirstSupine;
                string planId = planType == PlanType.Up ? "TMLIupperAuto" : "TMLIdownAuto";

                Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == courseId);
                ExternalPlanSetup newPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == planId);

                if (newPlan == null)
                {
                    StructureSet targetSS = GetTargetStructureSet(scriptContext, patientOrientation);
                    newPlan = targetCourse.AddExternalPlanSetup(targetSS);
                    newPlan.Id = planId;
                }

                scriptContext.PlanSetup = newPlan;  // set the plan in context
            });
        }

        public Task<bool> IsPlanDoseValidAsync(string courseId, string planId)
        {
            return this.esapiWorker.RunAsync(scriptContext => IsPlanDoseValid(scriptContext, courseId, planId), isWriteable: false);
        }

        public bool IsPlanDoseValid(PluginScriptContext scriptContext, string courseId, string planId)
        {
            Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == courseId);
            PlanSetup planSetup = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == planId);

            return planSetup.IsDoseValid;
        }

        public Task GenerateLowerJunctionAsync(string courseId,
                                               string upperPlanId,
                                               string lowerPlanId,
                                               string lowerPTVId,
                                               string registrationId,
                                               IProgress<double> progress,
                                               IProgress<string> message)
        {
            LowerJunction lowerJunction = new LowerJunction(this.esapiWorker, courseId, upperPlanId, lowerPlanId, lowerPTVId, registrationId);
            return lowerJunction.CreateAsync(progress, message);
        }

        public Task GenerateLowerControlAsync(string courseId,
                                              string lowerPlanId,
                                              string lowerPTVId,
                                              IProgress<double> progress,
                                              IProgress<string> message
                                              )
        {
            LowerControl lowerControl = new LowerControl(this.esapiWorker, courseId, lowerPlanId, lowerPTVId);
            return lowerControl.CreateAsync(progress, message);
        }

        public Task<string> GetMachineNameAsync(string courseId, string planId)
        {
            return this.esapiWorker.RunAsync(scriptContext => GetMachineName(scriptContext, courseId, planId), isWriteable: false);
        }

        public string GetMachineName(PluginScriptContext scriptContext, string courseId, string planId)
        {
            Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == courseId);
            PlanSetup selectedPlan = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == planId);
            return selectedPlan == null
                ? string.Empty
                : selectedPlan.Beams.Select(b => b.TreatmentUnit.Id).FirstOrDefault();
        }

        public Task OptimizeUpperAsync(string courseId,
                                       string upperPlanId,
                                       string upperPTVId,
                                       List<string> oarIds,
                                       IProgress<double> progress,
                                       IProgress<string> message)
        {
            UpperOptimization optimization = new UpperOptimization(this.esapiWorker,
                                                                   courseId,
                                                                   upperPlanId,
                                                                   upperPTVId,
                                                                   oarIds);
            return optimization.ComputeAsync(progress, message);
        }

        public Task OptimizeLowerAsync(string courseId,
                                       string upperPlanId,
                                       string registrationId,
                                       string lowerPlanId,
                                       IProgress<double> progress,
                                       IProgress<string> message)
        {
            LowerOptimization optimization = new LowerOptimization(this.esapiWorker,
                                                                   courseId,
                                                                   upperPlanId,
                                                                   registrationId,
                                                                   lowerPlanId);
            return optimization.ComputeAsync(progress, message);
        }

        public Task CreateScheduleCourseAsync()
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                Course newCourse = scriptContext.Patient.AddCourse();
                newCourse.Id = "CScheduleAuto";
                scriptContext.Course = newCourse;
            });
        }

        public Task SchedulePlansAsync(string courseId,
                                       string upperPlanId,
                                       string lowerPlanId,
                                       string scheduleCourseId,
                                       bool isocentersOnArms,
                                       List<string> scheduleSSStudySeriesId,
                                       IProgress<double> progress,
                                       IProgress<string> message)
        {
            Schedule schedule = new Schedule(this.esapiWorker,
                                             courseId,
                                             upperPlanId,
                                             lowerPlanId,
                                             scheduleCourseId,
                                             isocentersOnArms,
                                             scheduleSSStudySeriesId);
            return schedule.ComputeAsync(progress, message);
        }

        public Task ComputeDisplacementsAsync(string courseId,
                                         string upperPlanId,
                                         string lowerPlanId,
                                         string scheduleCourseId,
                                         DateTime treatmentDate,
                                         bool isocentersOnArms,
                                         IProgress<double> progress,
                                         IProgress<string> message)
        {
            Displacements schedule = new Displacements(this.esapiWorker,
                                                       courseId,
                                                       upperPlanId,
                                                       lowerPlanId,
                                                       scheduleCourseId,
                                                       treatmentDate,
                                                       isocentersOnArms);
            return schedule.ComputeAsync(progress, message);
        }
    }
}