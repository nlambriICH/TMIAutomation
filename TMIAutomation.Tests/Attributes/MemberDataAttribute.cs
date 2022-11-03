namespace TMIAutomation.Tests.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
    public class MemberDataAttribute : System.Attribute
    {
        private readonly string memberName;
        public string MemberName { get => memberName; private set { } }
        public MemberDataAttribute(string memberName)
        {
            this.memberName = memberName;
        }
    }
}
