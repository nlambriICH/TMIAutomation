﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIAutomation.Async;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation.StructureCreation
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

        public Task<List<string>> GetPlansAsync(PlanType planType)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
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
            },
            isWriteable: false);
        }

        public Task<List<string>> GetPTVsFromPlanAsync(string planId)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                PlanSetup selectedPlan = targetCourse.PlanSetups.FirstOrDefault(ps => ps.Id == planId);
                return selectedPlan == null ? new List<string>()
                : selectedPlan.StructureSet.Structures.Where(s => s.DicomType == "PTV")
                                                      .OrderByDescending(s => s.Volume)
                                                      .Select(s => s.Id)
                                                      .ToList();

            },
            isWriteable: false);
        }

        public Task<List<string>> GetPTVsFromImgOrientationAsync(PatientOrientation patientOrientation)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                StructureSet targetSS = this.GetTargetStructureSet(scriptContext, patientOrientation);
                return targetSS == null ? new List<string>()
                : targetSS.Structures.Where(s => s.DicomType == "PTV")
                         .OrderByDescending(s => s.Volume)
                         .Select(s => s.Id)
                         .ToList();
            },
            isWriteable: false);
        }

        private StructureSet GetTargetStructureSet(PluginScriptContext scriptContext, PatientOrientation patientOrientation)
        {
            if (scriptContext.StructureSet != null && scriptContext.StructureSet.Image.ImagingOrientation == patientOrientation)
            {
                return scriptContext.StructureSet;
            }
            else
            {
                return scriptContext.Patient.StructureSets.Where(ss => ss.Image.ImagingOrientation == patientOrientation)
                                              .OrderByDescending(ss => ss.HistoryDateTime)
                                              .FirstOrDefault();
            }
        }

        public Task<List<string>> GetRegistrationsAsync()
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                return scriptContext.Patient.Registrations.OrderByDescending(reg => reg.CreationDateTime)
                                                          .Select(reg => reg.Id)
                                                          .ToList();
            },
            isWriteable: false);
        }

        public Task GenerateUpperJunctionAsync(string upperPlanId, string upperPTVId, IProgress<double> progress, IProgress<string> message)
        {
            UpperJunction upperJunction = new UpperJunction(this.esapiWorker, upperPlanId, upperPTVId);
            return upperJunction.CreateAsync(progress, message);
        }

        public Task GenerateUpperControlAsync(string upperPlanId, string upperPTVId, IProgress<double> progress, IProgress<string> message)
        {
            UpperControl upperControl = new UpperControl(this.esapiWorker, upperPlanId, upperPTVId);
            return upperControl.CreateAsync(progress, message);
        }

        public Task GenerateLowerPlanAsync()
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                StructureSet targetSS = GetTargetStructureSet(scriptContext, PatientOrientation.FeetFirstSupine);

                ExternalPlanSetup newPlan = targetCourse.AddExternalPlanSetup(targetSS);
                int numOfAutoPlans = targetCourse.PlanSetups.Count(p => p.Id.Contains("TMLIdownAuto"));
                newPlan.Id = numOfAutoPlans == 0 ? "TMLIdownAuto" : string.Concat("TMLIdownAuto", numOfAutoPlans);
            });
        }

        public Task<bool> IsPlanDoseValid(string planId)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                PlanSetup upperPlan = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == planId);

                return upperPlan.IsDoseValid;
            });
        }

        public Task GenerateLowerJunctionAsync(string upperPlanId,
                                               string lowerPlanId,
                                               string lowerPTVId,
                                               string registrationId,
                                               IProgress<double> progress,
                                               IProgress<string> message)
        {
            LowerJunction lowerJunction = new LowerJunction(this.esapiWorker, upperPlanId, lowerPlanId, lowerPTVId, registrationId);
            return lowerJunction.CreateAsync(progress, message);
        }

        public Task GenerateLowerControlAsync(string lowerPlanId,
                                              string lowerPTVId,
                                              IProgress<double> progress,
                                              IProgress<string> message)
        {
            LowerControl lowerControl = new LowerControl(this.esapiWorker, lowerPlanId, lowerPTVId);
            return lowerControl.CreateAsync(progress, message);
        }

        public Task<string> GetMachineNameAsync(string planId)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                PlanSetup selectedPlan = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == planId);
                return selectedPlan == null ? string.Empty
                : selectedPlan.Beams.Select(b => b.TreatmentUnit.Id).FirstOrDefault();
            });
        }

        public Task OptimizeAsync(string lowerPlanId,
                                  string machineName,
                                  IProgress<double> progress,
                                  IProgress<string> message)
        {
            Optimization optimization = new Optimization(this.esapiWorker, lowerPlanId, machineName);
            return optimization.ComputeAsync(progress, message);
        }
    }
}