using System;
using System.Collections.Generic;
using System.Linq;
using TMIAutomation.Language;
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

        public override ITestBase Init(object testObject, params object[] optParams)
        {
            this.modelBase = testObject as ModelBase;
            this.scriptContext = optParams.OfType<PluginScriptContext>().FirstOrDefault();
            return this.scriptContext == null
                ? throw new ArgumentException($"A PluginScriptContext must be provided to instantiate {this.GetType()}")
                : this;
        }

        [Theory]
        [MemberData(nameof(GetCourses_Data))]
        private void GetCourses(bool schedule, List<string> expectedCourses)
        {
            List<string> courses = modelBase.GetCourses(scriptContext, schedule);
            Assert.Equal(expectedCourses, courses);
        }

        public static IEnumerable<object[]> GetCourses_Data()
        {
#if ESAPI16
            yield return new object[] { false, new List<string> { "CDemoTest", "CScheduling", "CBaseDoseAddOpt", "CBaseDoseAddOpt_", "CLowerAutoAddOpt", "TEst", "CBaseDoseAF", "CBaseDose", "CLowerAuto", "CDemo", "CJunction", "C1" } };
            yield return new object[] { true, new List<string> { "CScheduling", "CDemoTest", "C1", "CJunction", "CLowerAuto", "CDemo", "CBaseDoseAF", "CBaseDose", "CBaseDoseAddOpt_", "TEst", "CLowerAutoAddOpt", "CBaseDoseAddOpt", Resources.NewCourseListBox } };
#else
            yield return new object[] { false, new List<string> { "CDemoTest", "CNoPlan", "CScheduling", "CDemo", "LowerAuto", "CJunction", "C1" } };
            yield return new object[] { true, new List<string> { "CScheduling", "CDemoTest", "C1", "CDemo", "CJunction", "CNoPlan", "LowerAuto", Resources.NewCourseListBox } };
#endif
        }

        [Theory]
        [MemberData(nameof(GetPlans_Data))]
        private void GetPlans(ModelBase.PlanType planType, int index, string expectedPlanId)
        {
            List<string> plans = modelBase.GetPlans(scriptContext, scriptContext.Course.Id, planType);
            try
            {
                Assert.Equal(expectedPlanId, plans[index]);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {planType}, {index}, {expectedPlanId}", e);
            }
        }

        public static IEnumerable<object[]> GetPlans_Data()
        {
            yield return new object[] { ModelBase.PlanType.Up, 0, "RA_TMLIup5" };
            yield return new object[] { ModelBase.PlanType.Up, 1, "RA_TMLIup4" };
            yield return new object[] { ModelBase.PlanType.Up, 2, "RA_TMLIup3" };
            yield return new object[] { ModelBase.PlanType.Down, 0, "TMLIdownAuto" };
            yield return new object[] { ModelBase.PlanType.Down, 1, "TMLIdownAuto1" };
        }

        [Fact]
        private void GetSSStudySeriesId()
        {
#if ESAPI16
            List<string> expectedListId = new List<string>
            {
                "1\t5287 / Series5", "2\t5287 / Series5", "3\t5287 / Series5", "4\t5287 / Series5", "5\t5287 / Series5", "6\t5289 / Series1", "7\t5289 / Series1",
                "Iso_1\t5287 / Series5", "Iso_2\t5287 / Series5", "Iso_3\t5287 / Series5", "Iso_4\t5287 / Series5", "Iso_5\t5287 / Series5", "Iso_6\t5289 / Series1", "Iso_7\t5289 / Series1",
                "kVCBCT_01b01\t5287 / Series3", "kVCBCT_01c01\t5287 / Series4", "kVCBCT_01d01\t5287 / Series", "kVCBCT_01e01\t5287 / Series1", "kVCBCT_01f01\t5287 / Series2", "kVCBCT_01g01\t5289 / Series2", "kVCBCT_01h01\t5289 / Series",
                "CT_1\t5289 / Series1", "CT_2\t5289 / Series1", "CT_1\t5289 / Series1",
                "Junction_Auto\t5289 / Series1", "Lower_demo\t5289 / Series1", "LowerAuto\t5289 / Series1",
                "CLowerAutoAddOpt\t5289 / Series1", "LowerAuto1\t5289 / Series1", "LowerBaseDose\t5289 / Series1",
                "CT_2\t5287 / Series5", "CT_1\t5287 / Series5", "CT_1\t5287 / Series5",
                "Upper_test\t5287 / Series5", "TEST\t5287 / Series5",
            };
#else
            List<string> expectedListId = new List<string>
            {
                "1\t5287 / Series5", "2\t5287 / Series5", "3\t5287 / Series5", "4\t5287 / Series5", "5\t5287 / Series5", "6\t5289 / Series2", "7\t5289 / Series2",
                "Iso_1\t5287 / Series5", "Iso_2\t5287 / Series5", "Iso_3\t5287 / Series5", "Iso_4\t5287 / Series5", "Iso_5\t5287 / Series5", "Iso_6\t5289 / Series2", "Iso_7\t5289 / Series2",
                "kVCBCT_01b01\t5287 / Series4", "kVCBCT_01c01\t5287 / Series3", "kVCBCT_01d01\t5287 / Series1", "kVCBCT_01e01\t5287 / Series2", "kVCBCT_01f01\t5287 / Series", "kVCBCT_01g01\t5289 / Series1", "kVCBCT_01h01\t5289 / Series",
                "Lower_video_rec\t5289 / Series2", "CT_1\t5289 / Series2", "CT_2\t5289 / Series2", "CT_1\t5289 / Series2",
                "Junction_Auto\t5289 / Series2", "Lower_demo\t5289 / Series2", "LowerAuto\t5289 / Series2",
                "CT_2\t5287 / Series5", "CT_1\t5287 / Series5", "Upper_test\t5287 / Series5", "CT_1\t5287 / Series5", "TEST\t5287 / Series5",
            };
#endif

            List<string> listId = modelBase.GetSSStudySeriesId(scriptContext);
            try
            {
                Assert.Equal(expectedListId, listId);
            }
            catch (EqualException e)
            {
                throw new Exception("Unexpected upper field configuration", e);
            }
        }

        [Theory]
        [MemberData(nameof(GetPTVsFromPlan_Data))]
        private void GetPTVsFromPlan(string planId, int index, string expectedPTVId)
        {
            List<string> ptvs = modelBase.GetPTVsFromPlan(scriptContext, scriptContext.Course.Id, planId);
            try
            {
                Assert.Equal(expectedPTVId, ptvs[index]);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {planId}, {index}, {expectedPTVId}", e);
            }
        }

        public static IEnumerable<object[]> GetPTVsFromPlan_Data()
        {
            yield return new object[] { "RA_TMLIup4", 0, "PTV_totFIN" };
            yield return new object[] { "RA_TMLIup4", 1, "UpperPTVNoJ" };
            yield return new object[] { "RA_TMLIup3", 0, "PTV_totFIN" };
            yield return new object[] { "RA_TMLIup3", 1, "PTV_totFIN_Crop" };
            yield return new object[] { "TMLIdownAuto", 0, "PTV_Tot_Start" };
            yield return new object[] { "TMLIdownAuto", 1, "PTV_Total" };
            yield return new object[] { "", 0, "PTV_Tot_Start" };
        }

        [Theory]
        [MemberData(nameof(GetPTVsFromImgOrientation_Data))]
        private void GetPTVsFromImgOrientation(PatientOrientation patientOrientation, int index, string expectedPTVId)
        {
            List<string> ptvs = modelBase.GetPTVsFromImgOrientation(scriptContext, patientOrientation);
            try
            {
                Assert.Equal(expectedPTVId, ptvs[index]);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {patientOrientation}, {index}, {expectedPTVId}", e);
            }
        }

        public static IEnumerable<object[]> GetPTVsFromImgOrientation_Data()
        {
            yield return new object[] { PatientOrientation.HeadFirstSupine, 0, "PTV_totFIN" };
            yield return new object[] { PatientOrientation.FeetFirstSupine, 0, "PTV_Tot_Start" };
            yield return new object[] { PatientOrientation.FeetFirstSupine, 1, "PTV_Total" };
        }

        [Theory]
        [MemberData(nameof(GetRegistrations_Data))]
        private void GetRegistrations(int index, string expectedRegId)
        {
            List<string> registrations = modelBase.GetRegistrations(scriptContext);
            try
            {
                Assert.Equal(expectedRegId, registrations[index]);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {index}, {expectedRegId}", e);
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
            bool isDoseValid = modelBase.IsPlanDoseValid(scriptContext, scriptContext.Course.Id, planId);
            try
            {
                Assert.Equal(expected, isDoseValid);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {planId}, {expected}", e);
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
        private void GetMachineName(string planId, string expectedMachineName)
        {
            string machineName = modelBase.GetMachineName(scriptContext, scriptContext.Course.Id, planId);
            try
            {
                Assert.Equal(expectedMachineName, machineName);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {planId}, {expectedMachineName}", e);
            }
        }

        public static IEnumerable<object[]> GetMachineName_Data()
        {
            yield return new object[] { "RA_TMLIup5", "TrueBeamSN1015" };
        }
    }
}