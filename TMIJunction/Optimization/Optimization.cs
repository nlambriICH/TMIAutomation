using System.Linq;
using VMS.TPS.Common.Model.API;

namespace TMIJunction
{
    class Optimization
    {
        private readonly string lowerPlan;

        public Optimization(string lowerPlan)
        {
            this.lowerPlan = lowerPlan;
        }

        public void Start(ScriptContext context)
        {
            ExternalPlanSetup externalPlanSetup = context.ExternalPlansInScope.FirstOrDefault(ps => ps.Id == lowerPlan);

            externalPlanSetup.OptimizationSetup(); // must set dose prescription before adding objectives

            OptimizationSetup optSetup = externalPlanSetup.OptimizationSetup;
            StructureSet ss = externalPlanSetup.StructureSet;

            optSetup.ClearObjectives();
            optSetup.AddPointObjectives(ss);
            optSetup.AddEUDObjectives(ss);
            optSetup.UseJawTracking = false;
            optSetup.AddAutomaticNormalTissueObjective(150);
            //optSetup.ExcludeStructuresFromOptimization(ss);

            externalPlanSetup.OptimizePlan(context.Patient.Id);
            externalPlanSetup.AdjustYJawToMLCShape();
            externalPlanSetup.CalculateDose(context.Patient.Id);
            externalPlanSetup.Normalize();

        }
    }
}
