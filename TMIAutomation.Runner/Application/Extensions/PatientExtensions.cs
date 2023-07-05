using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation.Runner
{
    public static class PatientExtensions
    {
        public static IEnumerable<PlanningItem> GetPlanningItems(this Patient patient)
        {
            IEnumerable<PlanSetup> plans = patient.Courses?.SelectMany(c => c.PlanSetups) ?? new List<PlanSetup>();
            IEnumerable<PlanSum> planSums = patient.Courses?.SelectMany(c => c.PlanSums) ?? new List<PlanSum>();
            return plans.Concat<PlanningItem>(planSums);
        }
    }
}