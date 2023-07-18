using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Serilog;
using TMIAutomation.Async;

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
        private readonly Client httpClient;

        public UpperOptimization(EsapiWorker esapiWorker,
                                 string courseId,
                                 string upperPlanId,
                                 string upperPTVId,
                                 List<string> oarIds,
                                 Client httpClient)
        {
            this.esapiWorker = esapiWorker;
            this.courseId = courseId;
            this.upperPlanId = upperPlanId;
            this.upperPTVId = upperPTVId;
            this.oarIds = oarIds;
            this.httpClient = httpClient;
        }

        public Task ComputeAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("UpperOptimization context: {@context}",
                                   new List<string> { this.courseId, this.upperPlanId, this.upperPTVId, string.Join(",", this.oarIds) });

                var fieldGeometry = this.httpClient.GetFieldGeometry(scriptContext.Patient.Id, this.upperPTVId, this.oarIds);
                // create VMAT arcs...
            });
        }

        
    }
}
