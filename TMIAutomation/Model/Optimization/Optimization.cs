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
#if ESAPI16
        private readonly string upperPlanId;
        private readonly string registrationId;
#endif
        private readonly string lowerPlanId;
        private readonly string machineName;

#if ESAPI16
        public Optimization(EsapiWorker esapiWorker, string upperPlanId, string registrationId, string lowerPlanId, string machineName)
        {
            this.esapiWorker = esapiWorker;
            this.upperPlanId = upperPlanId;
            this.registrationId = registrationId;
            this.lowerPlanId = lowerPlanId;
            this.machineName = machineName;
        }
#else
        public Optimization(EsapiWorker esapiWorker, string lowerPlanId, string machineName)
                {
                    this.esapiWorker = esapiWorker;
                    this.lowerPlanId = lowerPlanId;
                    this.machineName = machineName;
                }
#endif

        public Task ComputeAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
#if ESAPI16
                logger.Information("Optimization context: {@context}", new List<string> { this.upperPlanId, this.registrationId, this.lowerPlanId, this.machineName });
#else
                logger.Information("Optimization context: {@context}", new List<string> { this.lowerPlanId, this.machineName });
#endif
                Course targetCourse = scriptContext.Course ?? scriptContext.Patient.Courses.OrderBy(c => c.HistoryDateTime).Last();
                ExternalPlanSetup lowerPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.lowerPlanId);
#if ESAPI16
                ExternalPlanSetup lowerPlanBase = targetCourse.GenerateBasePlan(lowerPlan.StructureSet);
                ExternalPlanSetup upperPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.upperPlanId);
                Registration registration = scriptContext.Patient.Registrations.FirstOrDefault(reg => reg.Id == this.registrationId);

                progress.Report(0.05);
                message.Report("Copying caudal isocenter's fields of upper-body...");
                /* CopyPlanSetup creates a plan with correct fields but in HFS
                 * The dose is lost once the plan is changed to FFS
                 */
                lowerPlanBase.CopyCaudalIsocenter(upperPlan, registration, this.machineName);

                progress.Report(0.1);
                message.Report("Calculating dose of base-dose plan...");
                lowerPlanBase.SetupOptimization();
                bool baseDoseSuccess = lowerPlanBase.CalculatePlanDose();
                if (!baseDoseSuccess)
                {
                    logger.Error("An error occured during dose calculation");
                    throw new Exception("Dose calculation was not successful");
                }

                lowerPlan.BaseDosePlanningItem = lowerPlanBase;
#endif
                progress.Report(0.2);
                message.Report("Placing isocenters...");
                lowerPlan.SetIsocenters(this.machineName);

                lowerPlan.SetupOptimization(); // must set dose prescription before adding objectives

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
#if ESAPI16
                PlanSum planSum = targetCourse.CreatePlanSum(new List<PlanSetup> { lowerPlan, upperPlan }, lowerPlan.StructureSet.Image);
                planSum.Id = "PlanSumAuto";
#endif
            });

        }
    }
}
