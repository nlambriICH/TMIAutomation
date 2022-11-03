using System;
using System.Linq;
using System.Reflection;

namespace TMIAutomation.Tests
{
    public sealed class TestBuilder
    {
        private static TestBuilder testBuilderInstance = null;
        private TestBuilder()
        {
        }

        public static TestBuilder Create()
        {
            if (testBuilderInstance == null)
            {
                testBuilderInstance = new TestBuilder();
                return testBuilderInstance;
            }

            return testBuilderInstance;
        }

        public TestBuilder Add<T>(object testObject, params object[] optParams) where T : ITestBase, new()
        {
            T testBase = new T();
            // Check whether T has a private member of same type as testObject
            BindingFlags bFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            FieldInfo[] fieldInfo = testBase.GetType().GetFields(bFlags);
            if (fieldInfo.Any(fInfo => fInfo.FieldType == testObject.GetType()))
            {
                testBase.Init(testObject, optParams).DiscoverTests();
                return testBuilderInstance;
            }
            else
            {
                throw new ArgumentException($"Could not find a private testObject of type {testObject.GetType()} in {testBase.GetType()}");
            }
        }
    }
}
