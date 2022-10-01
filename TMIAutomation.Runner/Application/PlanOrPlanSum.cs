using System;

namespace TMIAutomation.Runner
{
    internal class PlanOrPlanSum : IEquatable<PlanOrPlanSum>
    {
        public PlanningItemType PlanType { get; set; }
        public string Id { get; set; }
        public string CourseId { get; set; }

        public bool Equals(PlanOrPlanSum other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return PlanType == other.PlanType && string.Equals(Id, other.Id) && string.Equals(CourseId, other.CourseId);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PlanOrPlanSum)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)PlanType;
                hashCode = (hashCode * 397) ^ (Id != null ? Id.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (CourseId != null ? CourseId.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(PlanOrPlanSum p1, PlanOrPlanSum p2)
        {
            return Equals(p1, p2);
        }

        public static bool operator !=(PlanOrPlanSum p1, PlanOrPlanSum p2)
        {
            return !(p1 == p2);
        }
    }
}
