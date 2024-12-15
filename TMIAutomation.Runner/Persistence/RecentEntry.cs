using System;
using System.Collections.Generic;
using System.Linq;

namespace TMIAutomation.Runner
{
    internal class RecentEntry : IEquatable<RecentEntry>
    {
        public string PatientId { get; set; }
        public List<PlanOrPlanSum> PlansAndPlanSumsInScope { get; set; }
        public PlanOrPlanSum ActivePlan { get; set; }

        public bool Equals(RecentEntry other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(PatientId, other.PatientId) &&
                   Enumerable.SequenceEqual(PlansAndPlanSumsInScope, other.PlansAndPlanSumsInScope) &&
                   Equals(ActivePlan, other.ActivePlan);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RecentEntry)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = PatientId != null ? PatientId.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ (PlansAndPlanSumsInScope != null ? PlansAndPlanSumsInScope.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ActivePlan != null ? ActivePlan.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(RecentEntry r1, RecentEntry r2)
        {
            return Equals(r1, r2);
        }

        public static bool operator !=(RecentEntry r1, RecentEntry r2)
        {
            return !(r1 == r2);
        }
    }
}