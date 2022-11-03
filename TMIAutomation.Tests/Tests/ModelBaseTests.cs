using System;
using System.Collections.Generic;
using System.Linq;
using TMIAutomation.StructureCreation;
using TMIAutomation.Tests.Attributes;
using VMS.TPS.Common.Model.Types;
using Xunit;
using Xunit.Sdk;

namespace TMIAutomation.Tests
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
            List<string> plans = modelBase.GetPlans(scriptContext, planType);
            try
            {
                Assert.Equal(expected, plans[index]);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {planType}, {index}", e);
            }
        }

        public static IEnumerable<object[]> GetPlans_Data()
        {
            yield return new object[] { ModelBase.PlanType.Up, 0, "RA_TMLIup5" };
            yield return new object[] { ModelBase.PlanType.Up, 1, "RA_TMLIup4" };
            yield return new object[] { ModelBase.PlanType.Up, 2, "RA_TMLIup3" };
            yield return new object[] { ModelBase.PlanType.Down, 0, "TMLIdownAuto1" };
            yield return new object[] { ModelBase.PlanType.Down, 1, "TMLIdownAuto" };
        }

        [Theory]
        [MemberData(nameof(GetPTVsFromPlan_Data))]
        private void GetPTVsFromPlan(string planId, int index, string expected)
        {
            List<string> ptvs = modelBase.GetPTVsFromPlan(scriptContext, planId);
            try
            {
                Assert.Equal(expected, ptvs[index]);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {planId}, {index}", e);
            }
        }

        public static IEnumerable<object[]> GetPTVsFromPlan_Data()
        {
            yield return new object[] { "RA_TMLIup4", 0, "PTV_totFIN" };
            yield return new object[] { "RA_TMLIup4", 1, "PTV_totFIN_Crop" };
            yield return new object[] { "RA_TMLIup3", 0, "PTV_totFIN" };
            yield return new object[] { "RA_TMLIup3", 1, "PTV_totFIN_Crop" };
            yield return new object[] { "TMLIdownAuto", 0, "PTV_Tot_Start" };
            yield return new object[] { "TMLIdownAuto", 1, "PTV_Total" };
        }

        [Theory]
        [MemberData(nameof(GetPTVsFromImgOrientation_Data))]
        private void GetPTVsFromImgOrientation(PatientOrientation patientOrientation, int index, string expected)
        {
            List<string> ptvs = modelBase.GetPTVsFromImgOrientation(scriptContext, patientOrientation);
            try
            {
                Assert.Equal(expected, ptvs[index]);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {patientOrientation}, {index}", e);
            }
        }

        public static IEnumerable<object[]> GetPTVsFromImgOrientation_Data()
        {
            yield return new object[] { PatientOrientation.HeadFirstSupine, 0, "PTV_totFIN" };
            yield return new object[] { PatientOrientation.HeadFirstSupine, 1, "PTV_totFIN_Crop" };
            yield return new object[] { PatientOrientation.FeetFirstSupine, 0, "PTV_Tot_Start" };
            yield return new object[] { PatientOrientation.FeetFirstSupine, 1, "PTV_Total" };
        }

        [Theory]
        [MemberData(nameof(GetRegistrations_Data))]
        private void GetRegistrations(int index, string expected)
        {
            List<string> registrations = modelBase.GetRegistrations(scriptContext);
            try
            {
                Assert.Equal(expected, registrations[index]);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {index}", e);
            }
        }

        public static IEnumerable<object[]> GetRegistrations_Data()
        {
            yield return new object[] { 0, "ONLINEMATCH_7" };
            yield return new object[] { 1, "ONLINEMATCH_6" };
            yield return new object[] { 4, "ONLINEMATCH_3" };
            yield return new object[] { 7, "REGISTRATION" };
        }

        [Theory]
        [MemberData(nameof(IsPlanDoseValid_Data))]
        private void IsPlanDoseValid(string planId, bool expected)
        {
            bool isDoseValid = modelBase.IsPlanDoseValid(scriptContext, planId);
            try
            {
                Assert.Equal(expected, isDoseValid);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {planId}", e);
            }
        }

        public static IEnumerable<object[]> IsPlanDoseValid_Data()
        {
            yield return new object[] { "RA_TMLIup5", true };
            yield return new object[] { "RA_TMLIup4", false };
            yield return new object[] { "RA_TMLIup3", true };
        }

        [Theory]
        [MemberData(nameof(GetMachineName_Data))]
        private void GetMachineName(string planId, string expected)
        {
            string machineName = modelBase.GetMachineName(scriptContext, planId);
            try
            {
                Assert.Equal(expected, machineName);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {planId}", e);
            }
        }

        public static IEnumerable<object[]> GetMachineName_Data()
        {
            yield return new object[] { "RA_TMLIup5", "TrueBeamSN1015" };
        }
    }
}
