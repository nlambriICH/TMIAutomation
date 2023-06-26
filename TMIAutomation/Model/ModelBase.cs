using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIAutomation.Async;
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

        public Task<List<string>> GetCoursesAsync()
        {
            return this.esapiWorker.RunAsync(scriptContext => GetCourses(scriptContext), isWriteable: false);
        }

        public List<string> GetCourses(PluginScriptContext scriptContext)
        {
            List<Course> orderedCourses = scriptContext.Patient.Courses.OrderByDescending(c => c.HistoryDateTime).ToList();

            Course courseInScope = scriptContext.Course;
            if (orderedCourses.Remove(courseInScope))
            {
                orderedCourses.Insert(0, courseInScope);
            }

            return orderedCourses.Select(c => c.Id).ToList();
        }

        public Task<List<string>> GetPlansAsync(string courseId, PlanType planType)
        {
            return this.esapiWorker.RunAsync(scriptContext => GetPlans(scriptContext, courseId, planType), isWriteable: false);
        }

        public List<string> GetPlans(PluginScriptContext scriptContext, string courseId, PlanType planType)
        {
            Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == courseId);
            List<string> orderedPlans = new List<string>();
            switch (planType)
            {
                case PlanType.Up:
                    orderedPlans = targetCourse.PlanSetups.Where(p => p.StructureSet.Image.ImagingOrientation == PatientOrientation.HeadFirstSupine)
                                        .OrderByDescending(p => p.CreationDateTime)
                                        .Select(s => s.Id)
                                        .ToList();
                    break;
                case PlanType.Down:
                    orderedPlans = targetCourse.PlanSetups.Where(p => p.StructureSet.Image.ImagingOrientation == PatientOrientation.FeetFirstSupine)
                                        .OrderByDescending(p => p.CreationDateTime)
                                        .Select(s => s.Id)
                                        .ToList();
                    break;
            }
            return orderedPlans;
        }

        public Task<List<string>> GetPTVsFromPlanAsync(string courseId, string planId)
        {
            return this.esapiWorker.RunAsync(scriptContext => GetPTVsFromPlan(scriptContext, courseId, planId), isWriteable: false);
        }

        public List<string> GetPTVsFromPlan(PluginScriptContext scriptContext, string courseId, string planId)
        {
            Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == courseId);
            PlanSetup selectedPlan = targetCourse.PlanSetups.FirstOrDefault(ps => ps.Id == planId);
            return selectedPlan == null ? new List<string>()
            : selectedPlan.StructureSet.Structures.Where(s => s.DicomType == "PTV")
                                                  .OrderByDescending(s => s.Volume)
                                                  .Select(s => s.Id)
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

        public Task GenerateLowerPlanAsync(string courseId)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == courseId);
                StructureSet targetSS = GetTargetStructureSet(scriptContext, PatientOrientation.FeetFirstSupine);

                ExternalPlanSetup newPlan = targetCourse.AddExternalPlanSetup(targetSS);
                int numOfAutoPlans = targetCourse.PlanSetups.Count(p => p.Id.Contains("TMLIdownAuto"));
                newPlan.Id = numOfAutoPlans == 0 ? "TMLIdownAuto" : string.Concat("TMLIdownAuto", numOfAutoPlans);
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
                                              IProgress<string> message)
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

#if ESAPI16
        public Task OptimizeAsync(string courseId,
                                  string upperPlanId,
                                  string registrationId,
                                  string lowerPlanId,
                                  IProgress<double> progress,
                                  IProgress<string> message)
        {
            Optimization optimization = new Optimization(this.esapiWorker, courseId, upperPlanId, registrationId, lowerPlanId);
            return optimization.ComputeAsync(progress, message);
        }
#else
        public Task OptimizeAsync(string courseId,
                                  string upperPlanId,
                                  string registrationId,
                                  string lowerPlanId,
                                  bool generateBaseDosePlanOnly,
                                  IProgress<double> progress,
                                  IProgress<string> message)
        {
            Optimization optimization = new Optimization(this.esapiWorker, courseId, upperPlanId, registrationId, lowerPlanId, generateBaseDosePlanOnly);
            return optimization.ComputeAsync(progress, message);
        }
#endif
    }
}