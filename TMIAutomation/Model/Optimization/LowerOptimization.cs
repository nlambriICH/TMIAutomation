using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Serilog;
using TMIAutomation.Async;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public class LowerOptimization
    {
        private readonly ILogger logger = Log.ForContext<LowerOptimization>();
        private readonly EsapiWorker esapiWorker;
        private readonly string courseId;
        private readonly string upperPlanId;
        private readonly string registrationId;
        private readonly string lowerPlanId;

        public LowerOptimization(EsapiWorker esapiWorker,
                            string courseId,
                            string upperPlanId,
                            string registrationId,
                            string lowerPlanId)
        {
            this.esapiWorker = esapiWorker;
            this.courseId = courseId;
            this.upperPlanId = upperPlanId;
            this.registrationId = registrationId;
            this.lowerPlanId = lowerPlanId;
        }

        public Task ComputeAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("LowerOptimization context: {@context}", new List<string> { this.courseId, this.upperPlanId, this.registrationId, this.lowerPlanId });
                Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.courseId);
                ExternalPlanSetup lowerPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.lowerPlanId);
                ExternalPlanSetup upperPlan = targetCourse.ExternalPlanSetups.FirstOrDefault(p => p.Id == this.upperPlanId);
                Registration registration = scriptContext.Patient.Registrations.FirstOrDefault(reg => reg.Id == this.registrationId);
#if ESAPI15
                if (ConfigOptOptions.BaseDosePlanning)
                {
                    GenerateBaseDosePlan(targetCourse, upperPlan, lowerPlan, registration, progress, message);
                    ConfigureLowerPlanSetup(upperPlan, lowerPlan, progress, message);
                }
                else
                {
                    PerformLowerPlanOptimizationCommon(targetCourse, upperPlan, lowerPlan, progress, message);
                }
#else
                ExternalPlanSetup lowerPlanBase = GenerateBaseDosePlan(targetCourse, upperPlan, lowerPlan, registration, progress, message);
                lowerPlan.BaseDosePlanningItem = lowerPlanBase;
                ExternalPlanSetup optimizedLowerPlan = PerformLowerPlanOptimizationCommon(targetCourse, upperPlan, lowerPlan, progress, message);
                PlanSum planSum = targetCourse.CreatePlanSum(new List<PlanSetup> { optimizedLowerPlan, upperPlan }, optimizedLowerPlan.StructureSet.Image);
                planSum.Id = "PSAutoOpt1";

                if (planSum.NeedAdditionalOptimizationCycle(OptimizationCycleTarget.LowerPTV_J))
                {
                    progress.Report(0.05);
                    message.Report("Performing additional optimization cycle...");
                    PlanSetup latestLowerPlan = planSum.PlanSetups.FirstOrDefault(ps => ps.TreatmentOrientation == PatientOrientation.FeetFirstSupine);
                    StructureSet lowerSS = latestLowerPlan.StructureSet;
                    Structure lowerPTVJ = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.LOWER_PTV_JUNCTION);
                    lowerSS.CreateIsodoseOptStructures(planSum,
                                                       lowerPTVJ,
                                                       OptimizationCycleTarget.LowerPTV_J,
                                                       logger,
                                                       message);
                    Structure isodose100PlanSum = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_100_PS);
                    if (isodose100PlanSum.Volume > 0)
                    {
                        ExternalPlanSetup lowerPlanAddOpt = targetCourse.CopyPlanSetup(latestLowerPlan) as ExternalPlanSetup;
                        lowerPlanAddOpt.Id = "LowerOptJ";
                        lowerPlanAddOpt.OptimizationSetup.ClearObjectives();
                        lowerPlanAddOpt.OptimizationSetup.AddPointObjectives(lowerSS);
                        lowerPlanAddOpt.OptimizationSetup.AddEUDObjectives(lowerSS);
                        lowerPlanAddOpt.OptimizationSetup.AddPointObjectivesAdditionalOptCycle(lowerSS,
                                                                                               lowerPlanAddOpt.TotalDose,
                                                                                               OptimizationCycleTarget.LowerPTV_J);
                        message.Report("Continue plan optimization with intermediate dose...");
                        progress.Report(0.10);
                        lowerPlanAddOpt.ContinueOptimization();
                        progress.Report(0.10);
                        message.Report("Calculating dose...");
                        lowerPlanAddOpt.CalculatePlanDose();

                        message.Report($"Normalize plan: {StructureHelper.LOWER_PTV_NO_JUNCTION}-98%=98%...");
                        lowerPlanAddOpt.Normalize(0.98 * lowerPlanAddOpt.TotalDose, 98);

                        planSum = targetCourse.CreatePlanSum(new List<PlanSetup> { lowerPlanAddOpt, upperPlan }, lowerPlanAddOpt.StructureSet.Image);
                        planSum.Id = "PSAutoOpt2";
                    }
                    else
                    {
                        logger.Warning("Could not perform additional optimization because {DOSE_100_PS} volume was {volume}", StructureHelper.DOSE_100_PS, isodose100PlanSum.Volume);
                    }
                }
#endif
            });
        }

        private ExternalPlanSetup GenerateBaseDosePlan(Course targetCourse,
                                                       ExternalPlanSetup upperPlan,
                                                       ExternalPlanSetup lowerPlan,
                                                       Registration registration,
                                                       IProgress<double> progress,
                                                       IProgress<string> message)
        {
            progress.Report(0.10);
            message.Report("Copying caudal isocenter's fields of upper-body...");
            /* Course.CopyPlanSetup creates a plan with correct fields but in HFS
             * The dose is lost once the plan is changed to FFS
             */
            ExternalPlanSetup lowerPlanBase = targetCourse.GetOrCreateBaseDosePlan(lowerPlan.StructureSet);
            lowerPlanBase.CopyCaudalIsocenter(upperPlan, registration);

            progress.Report(0.10);
            message.Report("Calculating dose of base-dose plan...");
            lowerPlanBase.SetupOptimization();
            lowerPlanBase.CalculatePlanDose();
            lowerPlanBase.PlanNormalizationValue = upperPlan.PlanNormalizationValue;

            return lowerPlanBase;
        }

        private ExternalPlanSetup PerformLowerPlanOptimizationCommon(Course targetCourse,
                                                                     ExternalPlanSetup upperPlan,
                                                                     ExternalPlanSetup lowerPlan,
                                                                     IProgress<double> progress,
                                                                     IProgress<string> message)
        {
            ConfigureLowerPlanSetup(upperPlan, lowerPlan, progress, message);

            progress.Report(0.05);
            message.Report("Optimizing plan...");
            lowerPlan.OptimizePlan();

            progress.Report(0.05);
            message.Report("Adjusting jaw y-size to MLC shape...");
            lowerPlan.AdjustYJawToMLCShape();

            progress.Report(0.10);
            message.Report("Calculating dose...");
            lowerPlan.CalculatePlanDose();

            message.Report($"Normalize plan: {StructureHelper.LOWER_PTV_NO_JUNCTION}-98%=98%...");
            lowerPlan.Normalize(new DoseValue(0.98 * lowerPlan.TotalDose.Dose, "Gy"), 98);

            if (!lowerPlan.NeedAdditionalOptimizationCycle(OptimizationCycleTarget.LowerPTVNoJ))
            {
                return lowerPlan;
            }
            else
            {
                progress.Report(0.05);
                message.Report("Performing additional optimization cycle...");
                ExternalPlanSetup lowerPlanAddOpt = targetCourse.CopyPlanSetup(lowerPlan) as ExternalPlanSetup;
                /* Error thrown by ExternalPlanSetup.dll:
                 * Plan IDs without a revision number have a maximum length of 13 characters.
                 * The revision number may not exceed 2 digits
                */
                lowerPlanAddOpt.Id = "LowerOptPTV";

                StructureSet lowerSSAddOpt = lowerPlanAddOpt.StructureSet;
                Structure lowerPTVNoJ = lowerSSAddOpt.Structures.FirstOrDefault(s => s.Id == StructureHelper.LOWER_PTV_NO_JUNCTION);
                lowerPlanAddOpt.PlanNormalizationValue = 100; // no plan normalization
                lowerPlanAddOpt.PlanNormalizationValue = lowerPlanAddOpt.GetDoseAtVolume(lowerPTVNoJ,
                                                                                         50,
                                                                                         VolumePresentation.Relative,
                                                                                         DoseValuePresentation.Relative).Dose; // 100% in Target Mean
                lowerSSAddOpt.CreateIsodoseOptStructures(lowerPlanAddOpt,
                                                         lowerPTVNoJ,
                                                         OptimizationCycleTarget.LowerPTVNoJ,
                                                         logger,
                                                         message);
                lowerPlanAddOpt.OptimizationSetup.AddPointObjectivesAdditionalOptCycle(lowerSSAddOpt,
                                                                                       lowerPlanAddOpt.TotalDose,
                                                                                       OptimizationCycleTarget.LowerPTVNoJ);
                progress.Report(0.10);
                message.Report("Continue plan optimization with intermediate dose...");
                lowerPlanAddOpt.ContinueOptimization();
                progress.Report(0.10);
                message.Report("Calculating dose...");
                lowerPlanAddOpt.CalculatePlanDose();

                message.Report($"Normalize plan: {StructureHelper.LOWER_PTV_NO_JUNCTION}-98%=98%...");
                lowerPlanAddOpt.Normalize(0.98 * lowerPlanAddOpt.TotalDose, 98);

                return lowerPlanAddOpt;
            }
        }

        private void ConfigureLowerPlanSetup(ExternalPlanSetup upperPlan,
                                             ExternalPlanSetup lowerPlan,
                                             IProgress<double> progress,
                                             IProgress<string> message
            )
        {
            progress.Report(0.05);
            message.Report("Setup isocenters and objectives...");
            lowerPlan.SetIsocentersLower(upperPlan);
            lowerPlan.SetupOptimization(); // must set dose prescription before adding objectives

            OptimizationSetup optSetup = lowerPlan.OptimizationSetup;
            StructureSet lowerSS = lowerPlan.StructureSet;

            optSetup.ClearObjectives();
            optSetup.AddPointObjectives(lowerSS);
            optSetup.AddEUDObjectives(lowerSS);
            optSetup.UseJawTracking = false;
            optSetup.AddAutomaticNormalTissueObjective(150);
            //optSetup.ExcludeStructuresFromOptimization(ss);
        }
    }
}