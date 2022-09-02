using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIJunction.Async;
using VMS.TPS.Common.Model.API;

namespace TMIJunction.StructureCreation
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
            return esapiWorker.RunAsync(scriptContext =>
            {
                Course latestCourse = scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                return latestCourse.PlanSetups.Where(p => p.Id.IndexOf(planType.ToString(), StringComparison.OrdinalIgnoreCase) >= 0)
                                  .OrderByDescending(p => p.CreationDateTime)
                                  .Select(p => p.Id)
                                  .ToList();
            },
            isWriteable: false);
        }

        public Task<List<string>> GetPTVsOfPlanAsync(string planId)
        {
            return esapiWorker.RunAsync(scriptContext =>
            {
                PlanSetup selectedPlan = scriptContext.Patient.Courses.SelectMany(c => c.PlanSetups).FirstOrDefault(ps => ps.Id == planId);

                return selectedPlan == null ? new List<string>()
                : selectedPlan.StructureSet.Structures
                                .Where(s => s.DicomType == "PTV")
                                .OrderByDescending(s => s.Volume)
                                .Select(p => p.Id)
                                .ToList();
            },
            isWriteable: false);
        }

        public Task<List<string>> GetRegistrationsAsync()
        {
            return esapiWorker.RunAsync(scriptContext =>
            {
                return scriptContext.Patient.Registrations.OrderByDescending(reg => reg.CreationDateTime)
                                                          .Select(reg => reg.Id)
                                                          .ToList();
            },
            isWriteable: false);
        }

        public Task GenerateUpperJunctionAsync(string upperPlanId, string upperPTVId, Progress<double> progress, Progress<string> message)
        {
            UpperJunction upperJunction = new UpperJunction(this.esapiWorker, upperPlanId, upperPTVId);
            return upperJunction.CreateAsync(progress, message);
        }

        public Task GenerateUpperControlAsync(string upperPlanId, string upperPTVId, Progress<double> progress, Progress<string> message)
        {
            UpperControl upperControl = new UpperControl(this.esapiWorker, upperPlanId, upperPTVId);
            return upperControl.CreateAsync(progress, message);
        }
    }
}
