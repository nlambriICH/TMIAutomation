namespace TMIAutomation.Tests.Attributes
{
    [System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
    public class InlineDataAttribute : System.Attribute
    {
        private readonly object[] data;
        public object[] Data { get => data; private set { } }
        public InlineDataAttribute(params object[] data)
        {
            this.data = data;
        }

    }
}