using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIJunction.Async;
using VMS.TPS.Common.Model.API;

namespace TMIJunction.StructureCreation
{
    public class BaseModel
    {
        private EsapiWorker esapiWorker;
        private LegsJunction legsJunction;
        private LegsControlStructures legsControlStructures;

        public BaseModel(EsapiWorker esapiWorker)
        {
            this.esapiWorker = esapiWorker;
            this.legsJunction = new LegsJunction(esapiWorker);
            this.legsControlStructures = new LegsControlStructures(esapiWorker);
        }

        public Task<List<string>> GetUpperPlansAsync()
        {
            return esapiWorker.RunAsync(scriptContext =>
            {
                Course latestCourse = scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                return latestCourse.PlanSetups.Where(p => p.Id.Contains("up"))
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
                return selectedPlan.StructureSet.Structures
                                .Where(s => s.DicomType == "PTV")
                                .OrderByDescending(s => s.Volume)
                                .Select(p => p.Id)
                                .ToList();
            },
            isWriteable: false);
        }

        public Task GenerateUpperJunctionAsync(string upperPlanId, string upperPTVId, Progress<double> progress, Progress<string> message)
        {
            BodyJunction bodyJunction = new BodyJunction(this.esapiWorker, upperPlanId, upperPTVId);
            return bodyJunction.CreateAsync(progress, message);
        }

        public Task GenerateUpperControlAsync(string upperPlanId, string upperPTVId, Progress<double> progress, Progress<string> message)
        {
            BodyControlStructures bodyControlStructures = new BodyControlStructures(this.esapiWorker, upperPlanId, upperPTVId);
            return bodyControlStructures.CreateAsync(progress, message);
        }
    }
}
