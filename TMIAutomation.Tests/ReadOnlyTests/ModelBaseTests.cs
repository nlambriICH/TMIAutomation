using System;
using System.Collections.Generic;
using System.Linq;
using TMIAutomation.StructureCreation;
using TMIAutomation.Tests.Attributes;
using Xunit;

namespace TMIAutomation.Tests.ReadOnlyTests
{
    public class ModelBaseTests : TestBase
    {
        private ModelBase modelBase;
        private PluginScriptContext scriptContext;
        private static bool firstInitialization = true;

        public override ITestBase Init(object testObject, params object[] optParams)
        {
            if (!firstInitialization)
            {
                throw new InvalidOperationException($"{this.GetType()} is already initialized");
            }
            firstInitialization = false;
            this.modelBase = testObject as ModelBase;
            this.scriptContext = optParams.OfType<PluginScriptContext>().FirstOrDefault();
            return this.scriptContext == null
                ? throw new ArgumentException($"A PluginScriptContext must be provided to instantiate {this.GetType()}")
                : this;
        }

        [Theory]
        [MemberData(nameof(GetPlans_Data))]
        private void GetPlans(ModelBase.PlanType planType, int index, string expected)
        {
            List<string> upperPlans = modelBase.GetPlans(scriptContext, planType);
            Assert.Equal(expected, upperPlans[index]);
        }

        public IEnumerable<object[]> GetPlans_Data()
        {
            yield return new object[] { ModelBase.PlanType.Up, 0, "RA_TMLIup4" };
            yield return new object[] { ModelBase.PlanType.Up, 1, "RA_TMLIup3" };
            yield return new object[] { ModelBase.PlanType.Down, 0, "TMLIdownAuto" };
        }

        [Theory]
        [MemberData(nameof(GetPTVsFromPlan_Data))]
        private void GetPTVsFromPlan(string planId, int index, string expected)
        {
            List<string> upperPlans = modelBase.GetPTVsFromPlan(scriptContext, planId);
            Assert.Equal(expected, upperPlans[index]);
        }

        public IEnumerable<object[]> GetPTVsFromPlan_Data()
        {
            yield return new object[] { "RA_TMLIup4", 0, "PTV_totFIN" };
            yield return new object[] { "RA_TMLIup4", 1, "PTV_totFIN_Crop" };
            yield return new object[] { "RA_TMLIup3", 0, "PTV_totFIN" };
            yield return new object[] { "RA_TMLIup3", 1, "PTV_totFIN_Crop" };
            yield return new object[] { "TMLIdownAuto", 0, "PTV_Tot_Start" };
            yield return new object[] { "TMLIdownAuto", 1, "PTV_Total" };
        }
    }
}
