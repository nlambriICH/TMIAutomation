namespace TMIAutomation.Tests
{
    public interface ITestBase
    {
        ITestBase Init(object testObject, params object[] optParams);
        ITestBase DiscoverTests();
    }
}
