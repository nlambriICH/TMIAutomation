using System;
using System.Collections.Generic;
using System.Linq;
using TMIAutomation.Tests.Attributes;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Xunit;
using Xunit.Sdk;

namespace TMIAutomation.Tests
{
    public class IsocenterTests : TestBase
    {
        private ExternalPlanSetup targetPlan;
        private PluginScriptContext scriptContext;

        public override ITestBase Init(object testObject, params object[] optParams)
        {
            this.targetPlan = testObject as ExternalPlanSetup;
            this.scriptContext = optParams.OfType<PluginScriptContext>().FirstOrDefault();
            return this.scriptContext == null
                ? throw new ArgumentException($"A PluginScriptContext must be provided to instantiate {this.GetType()}")
                : this;
        }

        [Theory]
        [MemberData(nameof(CopyCaudalIsocenter_Data))]
        private void CopyCaudalIsocenter(string sourcePlanId,
                                         string registrationId,
                                         int index,
                                         string expectedBeamId,
                                         GantryDirection expectedGantryDir,
                                         double expectedGantryAngleStart,
                                         double expectedGantryAngleStop,
                                         double expectedCollRtn)
        {
            ExternalPlanSetup sourcePlan = this.scriptContext.Course.ExternalPlanSetups.FirstOrDefault(ps => ps.Id == sourcePlanId);
            Registration registration = this.scriptContext.Patient.Registrations.FirstOrDefault(reg => reg.Id == registrationId);

            targetPlan.CopyCaudalIsocenter(sourcePlan, registration);

            Beam newBeam = targetPlan.Beams.ElementAt(index);
            try
            {
                Assert.Equal(expectedBeamId, newBeam.Id);
                Assert.Equal(expectedGantryDir, newBeam.GantryDirection);
                Assert.Equal(expectedGantryAngleStart, newBeam.ControlPoints.First().GantryAngle, 0.01);
                Assert.Equal(expectedGantryAngleStop, newBeam.ControlPoints.Last().GantryAngle, 0.01);
                Assert.Equal(expectedCollRtn, newBeam.ControlPoints.First().CollimatorAngle);
            }
            catch (EqualException e)
            {
                throw new Exception(
                    $"Input parameters: {sourcePlanId}, {registrationId}, {index}, " +
                    $"{expectedBeamId}, {expectedGantryDir}, {expectedGantryAngleStart}, " +
                    $"{expectedGantryAngleStop}, {expectedCollRtn}",
                    e);
            }
            finally
            {
                // Teardown
                targetPlan.Beams.ToList().ForEach(beam => targetPlan.RemoveBeam(beam));
            }
        }

        public static IEnumerable<object[]> CopyCaudalIsocenter_Data()
        {
            yield return new object[] { "RA_TMLIup3", "REGISTRATION", 0, "Field 9", GantryDirection.Clockwise, 180.1, 179.9, 175 };
            yield return new object[] { "RA_TMLIup3", "REGISTRATION", 1, "Field 10", GantryDirection.CounterClockwise, 179.9, 180.1, 185 };
        }
    }
}
