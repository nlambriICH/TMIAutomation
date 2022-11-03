namespace TMIAutomation.Tests
{
    public interface ITestBase
    {
        ITestBase Init(object testObject, params object[] optParams);
        void RunTests();
        ITestBase DiscoverTests();
    }
}
