using System;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation.Runner
{
    public static class PlanningItemExtensions
    {
        /// <summary>
        /// Gets the parent Course of the given PlanningItem.
        /// </summary>
        /// <param name="planningItem">The PlanningItem whose Course will be obtained.</param>
        /// <returns>The parent Course of the given PlanningItem.</returns>
        public static Course GetCourse(this PlanningItem planningItem)
        {
            switch (planningItem)
            {
                case PlanSetup plan:
                    return plan.Course;
                case PlanSum planSum:
                    return planSum.Course;
                default:
                    throw new ArgumentException(GetUnknownSubclassMessage(planningItem));
            }
        }

        /// <summary>
        /// Gets the StructureSet of the given PlanningItem.
        /// </summary>
        /// <param name="planningItem">The PlanningItem whose StructureSet will be obtained.</param>
        /// <returns>The StructureSet of the given PlanningItem.</returns>
        public static StructureSet GetStructureSet(this PlanningItem planningItem)
        {
            switch (planningItem)
            {
                case PlanSetup plan:
                    return plan.StructureSet;
                case PlanSum planSum:
                    return planSum.StructureSet;
                default:
                    throw new ArgumentException(GetUnknownSubclassMessage(planningItem));
            }
        }

        private static string GetUnknownSubclassMessage(PlanningItem planningItem)
        {
            return $"{planningItem.GetType()} is not a known subclass of {nameof(PlanningItem)}.";
        }
    }
}