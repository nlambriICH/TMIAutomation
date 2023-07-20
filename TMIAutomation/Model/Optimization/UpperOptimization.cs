using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using TMIAutomation.Async;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation
{
    public class UpperOptimization
    {
        private readonly ILogger logger = Log.ForContext<UpperOptimization>();
        private readonly EsapiWorker esapiWorker;
        private readonly string courseId;
        private readonly string upperPlanId;
        private readonly string upperPTVId;
        private readonly List<string> oarIds;

        public UpperOptimization(EsapiWorker esapiWorker,
                                 string courseId,
                                 string upperPlanId,
                                 string upperPTVId,
                                 List<string> oarIds)
        {
            this.esapiWorker = esapiWorker;
            this.courseId = courseId;
            this.upperPlanId = upperPlanId;
            this.upperPTVId = upperPTVId;
            this.oarIds = oarIds;
        }

        public Task ComputeAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("UpperOptimization context: {@context}",
                                   new List<string> { this.courseId, this.upperPlanId, this.upperPTVId, string.Join(",", this.oarIds) });

                progress.Report(0.50);
                message.Report("Get model predictions...");
                Dictionary<string, List<List<double>>> fieldGeometry = Client.GetFieldGeometry(scriptContext.Patient.Id, this.upperPTVId, this.oarIds);
                
                Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.courseId);
                ExternalPlanSetup upperPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.upperPlanId);

                progress.Report(0.40);
                message.Report("Set isocenters...");
                upperPlan.SetIsocentersUpper(fieldGeometry);
            });
        }

        
    }
}
