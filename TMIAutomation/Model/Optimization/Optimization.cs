using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIAutomation.Async;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation
{
    public class Optimization
    {
        private readonly ILogger logger = Log.ForContext<Optimization>();
        private readonly EsapiWorker esapiWorker;
        private readonly string lowerPlanId;
        private readonly string machineName;

        public Optimization(EsapiWorker esapiWorker, string lowerPlanId, string machineName)
        {
            this.esapiWorker = esapiWorker;
            this.lowerPlanId = lowerPlanId;
            this.machineName = machineName;
        }

        public Task ComputeAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("Optimization context: {@context}", new List<string> { this.lowerPlanId, this.machineName });
                Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                ExternalPlanSetup lowerPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == lowerPlanId);

                progress.Report(0.2);
                message.Report("Placing isocenters...");
                lowerPlan.SetIsocenters(this.machineName);

                lowerPlan.OptimizationSetup(); // must set dose prescription before adding objectives

                OptimizationSetup optSetup = lowerPlan.OptimizationSetup;
                StructureSet lowerSS = lowerPlan.StructureSet;

                optSetup.ClearObjectives();
                optSetup.AddPointObjectives(lowerSS);
                optSetup.AddEUDObjectives(lowerSS);
                optSetup.UseJawTracking = false;
                optSetup.AddAutomaticNormalTissueObjective(150);
                //optSetup.ExcludeStructuresFromOptimization(ss);

                progress.Report(0.35);
                message.Report("Optimizing plan...");
                bool optSuccess = lowerPlan.OptimizePlan();
                if (!optSuccess)
                {
                    logger.Error("An error occured during optimization");
                    throw new Exception("Optimization was not successful");
                }

                message.Report("Adjusting jaw y-size to MLC shape...");
                lowerPlan.AdjustYJawToMLCShape();

                progress.Report(0.35);
                message.Report("Calculating dose...");
                bool doseSuccess = lowerPlan.CalculatePlanDose();
                if (!doseSuccess)
                {
                    logger.Error("An error occured during dose calculation");
                    throw new Exception("Dose calculation was not successful");
                }

                message.Report($"Normalize plan: {StructureHelper.LOWER_PTV_NO_JUNCTION}-98%=98%...");
                lowerPlan.Normalize();
            });

        }
    }
}
