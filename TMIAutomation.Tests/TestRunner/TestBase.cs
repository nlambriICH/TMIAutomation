using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMIAutomation.Tests.Attributes;

namespace TMIAutomation.Tests
{
    public abstract class TestBase : ITestBase
    {
        public ITestBase DiscoverTests()
        {
            BindingFlags bFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (MethodInfo method in this.GetType().GetMethods(bFlags))
            {
                if (method.GetCustomAttributes(typeof(FactAttribute)).Any())
                {
                    if (DiscoveredTests.FactTests.ContainsKey(this))
                    {
                        DiscoveredTests.FactTests[this].Add(method);
                    }
                    else
                    {
                        DiscoveredTests.FactTests.Add(this, new List<MethodInfo> { method });
                    }
                }
                else if (method.GetCustomAttributes(typeof(TheoryAttribute)).Any())
                {
                    if (DiscoveredTests.TheoryTests.ContainsKey(this))
                    {
                        DiscoveredTests.TheoryTests[this].Add(method);
                    }
                    else
                    {
                        DiscoveredTests.TheoryTests.Add(this, new List<MethodInfo> { method });
                    }
                }
            }
            return this;
        }

        public void RunTests()
        {
            foreach (KeyValuePair<object, List<MethodInfo>> factTests in DiscoveredTests.FactTests)
            {
                object testInstance = factTests.Key;
                foreach (MethodInfo factTest in factTests.Value)
                {
                    try
                    {
                        TestReporter.TotalTests++;
                        factTest.Invoke(testInstance, new object[] { });
                        TestReporter.PassedTests++;
                    }
                    catch (Exception e)
                    {
                        TestReporter.ReportFailedTest(testInstance, factTest, e);
                    }
                }
            }
            foreach (KeyValuePair<object, List<MethodInfo>> theoryTests in DiscoveredTests.TheoryTests)
            {
                object testInstance = theoryTests.Key;
                foreach (MethodInfo theoryTest in theoryTests.Value)
                {
                    foreach (InlineDataAttribute attribute in theoryTest.GetCustomAttributes(typeof(InlineDataAttribute)))
                    {
                        try
                        {
                            TestReporter.TotalTests++;
                            theoryTest.Invoke(testInstance, attribute.Data);
                            TestReporter.PassedTests++;
                        }
                        catch (Exception e)
                        {
                            TestReporter.ReportFailedTest(testInstance, theoryTest, e);
                        }
                    }

                    MemberDataAttribute memberDataAttribute = (MemberDataAttribute)theoryTest.GetCustomAttribute(typeof(MemberDataAttribute));
                    if (memberDataAttribute != null)
                    {
                        object testDataGenerator = testInstance.GetType().GetMethod(memberDataAttribute.MemberName).Invoke(this, null);
                        if (testDataGenerator is IEnumerable<object[]> dataItems)
                        {
                            foreach (object[] data in dataItems)
                            {
                                try
                                {
                                    TestReporter.TotalTests++;
                                    theoryTest.Invoke(testInstance, data);
                                    TestReporter.PassedTests++;
                                }
                                catch (Exception e)
                                {
                                    TestReporter.ReportFailedTest(testInstance, theoryTest, e);
                                }
                            }
                        }
                    }
                }
            }

            TestReporter.PrintReport();
        }

        public abstract ITestBase Init(object testObject, params object[] optParams);
    }
}
