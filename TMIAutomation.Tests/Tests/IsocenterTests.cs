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
        private ExternalPlanSetup planInContext;
        private ExternalPlanSetup upperPlan;
        private PluginScriptContext scriptContext;

        public override ITestBase Init(object testObject, params object[] optParams)
        {
            this.planInContext = testObject as ExternalPlanSetup;
            this.upperPlan = optParams.OfType<PlanSetup>().FirstOrDefault() as ExternalPlanSetup;
            this.scriptContext = optParams.OfType<PluginScriptContext>().FirstOrDefault();
            return this.scriptContext == null
                ? throw new ArgumentException($"A PluginScriptContext must be provided to instantiate {this.GetType()}")
                : this;
        }

        [Theory]
        [MemberData(nameof(CopyCaudalIsocenter_Data))]
        private void CopyCaudalIsocenter(string sourcePlanId,
                                         string registrationId,
                                         string expectedBeamId,
                                         GantryDirection expectedGantryDir,
                                         double expectedGantryAngleStart,
                                         double expectedGantryAngleStop,
                                         double expectedCollRtn)
        {
            ExternalPlanSetup sourcePlan = this.scriptContext.Course.ExternalPlanSetups.FirstOrDefault(ps => ps.Id == sourcePlanId);
            Registration registration = this.scriptContext.Patient.Registrations.FirstOrDefault(reg => reg.Id == registrationId);

            planInContext.CopyCaudalIsocenter(sourcePlan, registration);

            Beam newBeam = planInContext.Beams.FirstOrDefault(b => b.Id == expectedBeamId);
            try
            {
                Assert.Equal(expectedGantryDir, newBeam.GantryDirection);
                Assert.Equal(expectedGantryAngleStart, newBeam.ControlPoints.First().GantryAngle, 0.01);
                Assert.Equal(expectedGantryAngleStop, newBeam.ControlPoints.Last().GantryAngle, 0.01);
                Assert.Equal(expectedCollRtn, newBeam.ControlPoints.First().CollimatorAngle);
            }
            catch (EqualException e)
            {
                throw new Exception(
                    $"Input parameters: {sourcePlanId}, {registrationId}, " +
                    $"{expectedBeamId}, {expectedGantryDir}, {expectedGantryAngleStart}, " +
                    $"{expectedGantryAngleStop}, {expectedCollRtn}",
                    e);
            }
            finally
            {
                // Teardown
                planInContext.Beams.ToList().ForEach(beam => planInContext.RemoveBeam(beam));
            }
        }

        public static IEnumerable<object[]> CopyCaudalIsocenter_Data()
        {
            yield return new object[] { "RA_TMLIup3", "REGISTRATION", "Field 9", GantryDirection.Clockwise, 180.1, 179.9, 175 };
            yield return new object[] { "RA_TMLIup3", "REGISTRATION", "Field 10", GantryDirection.CounterClockwise, 179.9, 180.1, 185 };
        }

        [Fact]
        private void SetIsocentersUpperBodyCNN()
        {
            List<List<double>> isocenters = new List<List<double>>
            {
                new List<double> { 26.1, 118.0, -858.4 }, // pelvis
                new List<double> { 26.1, 118.0, -858.4 },
                new List<double> { 26.1, 118.0, -629.0 }, // abdomen
                new List<double> { 26.1, 118.0, -629.0 },
                new List<double> { 26.1, 118.0, -417.7 }, // thorax
                new List<double> { 26.1, 118.0, -417.7 },
                new List<double> { 26.1, 118.0, -206.3 }, // shoulders
                new List<double> { 26.1, 118.0, -206.3 },
                new List<double> { 26.1, 118.0, -1.8 },  // head
                new List<double> { 26.1, 118.0, -1.8 },
                new List<double> { -300, 118.0, 163.4 }, // arms
                new List<double> { -300, 118.0, 163.4 },
            };
            List<List<double>> jawX = new List<List<double>>
            {
                new List<double> { -19.1, 168.2 }, // pelvis
                new List<double> { -169.8, 20.8 },
                new List<double> { -9.1, 97.4 }, // abdomen
                new List<double> { -130.0, 8.8 },
                new List<double> { -10.7, 133.7 }, // thorax
                new List<double> { -123.9, 10.7 },
                new List<double> { -9.2, 110.1 }, // shoulders
                new List<double> { -127.8, 9.2 },
                new List<double> { -9.6, 130.5 }, // head
                new List<double> { -120.3, 9.6 },
                new List<double> { -300, -300 }, // arms
                new List<double> { -300, -300 },
            };
            List<List<double>> jawY = new List<List<double>>
            {
                new List<double> { -200, 200 }, // pelvis
                new List<double> { -200, 200 },
                new List<double> { -200, 200 }, // abdomen
                new List<double> { -200, 200 },
                new List<double> { -200, 200 }, // thorax
                new List<double> { -200, 200 },
                new List<double> { -200, 200 }, // shoulders
                new List<double> { -200, 200 },
                new List<double> { -128.2, 132.4 }, // head
                new List<double> { -116.8, 122.1 },
                new List<double> { -163.4, -163.4 }, // arms
                new List<double> { -163.4, -163.4 },
            };
            Dictionary<string, List<List<double>>> fieldGeometry = new Dictionary<string, List<List<double>>>
            {
                { "Isocenters", isocenters },
                { "Jaw_X", jawX },
                { "Jaw_Y", jawY },
            };
            Structure upperPTV = this.upperPlan.StructureSet.Structures.FirstOrDefault(s => s.Id == StructureHelper.UPPER_PTV_NO_JUNCTION);
            upperPlan.SetIsocentersUpper(Client.MODEL_NAME_BODY, upperPTV, fieldGeometry);

            try
            {
                foreach (Beam beam in upperPlan.Beams)
                {
                    if (beam.BeamNumber % 2 != 0)
                    {
                        Assert.Equal(179.9, beam.ControlPoints.First().GantryAngle);
                        Assert.Equal(180.1, beam.ControlPoints.Last().GantryAngle);
                        Assert.Equal(GantryDirection.CounterClockwise, beam.GantryDirection);
                    }
                    else
                    {
                        Assert.Equal(180.1, beam.ControlPoints.First().GantryAngle);
                        Assert.Equal(179.9, beam.ControlPoints.Last().GantryAngle);
                        Assert.Equal(GantryDirection.Clockwise, beam.GantryDirection);
                    }
                }
            }
            catch (EqualException e)
            {
                throw new Exception("Unexpected upper field configuration", e);
            }
            finally
            {
                // Teardown
                upperPlan.Beams.ToList().ForEach(beam => planInContext.RemoveBeam(beam));
            }
        }

        [Fact]
        private void SetIsocentersUpperArmsCNN()
        {
            List<List<double>> isocenters = new List<List<double>>
            {
                new List<double> { 26.0, 118.0, -811.5 }, // pelvis
                new List<double> { 26.0, 118.0, -811.5 },
                new List<double> { 26.0, 118.0, -531.6 }, // abdomen
                new List<double> { 26.0, 118.0, -531.6 },
                new List<double> { 26.0, 118.0, 163.4 }, // thorax
                new List<double> { 26.0, 118.0, 163.4 },
                new List<double> { 26.0, 118.0, -260.6 }, // shoulders
                new List<double> { 26.0, 118.0, -260.6 },
                new List<double> { 26.0, 118.0, -16.7 },  // head
                new List<double> { 26.0, 118.0, -16.7 },
                new List<double> { -185.5, 118.0, -510.2 }, // arms
                new List<double> { 201.9, 118.0, -510.2 },
            };
            List<List<double>> jawX = new List<List<double>>
            {
                new List<double> { -28.7, 153.7 }, // pelvis
                new List<double> { -160.3, 13.1 },
                new List<double> { -5.0, 133.6 }, // abdomen
                new List<double> { -142.3, 0.0 },
                new List<double> { 26.0, 26.0 }, // thorax
                new List<double> { 26.0, 26.0 },
                new List<double> { -10.2, 161.9 }, // shoulders
                new List<double> { -143.7, 10.2 },
                new List<double> { -10.9, 119.6 }, // head
                new List<double> { -120.8, 10.9 },
                new List<double> { -80.1, 64.3 }, // arms
                new List<double> { -75.1, 78.4 },
            };
            List<List<double>> jawY = new List<List<double>>
            {
                new List<double> { -200, 200 }, // pelvis
                new List<double> { -200, 200 },
                new List<double> { -200, 200 }, // abdomen
                new List<double> { -200, 200 },
                new List<double> { -163.4, -163.4 }, // thorax
                new List<double> { -163.4, -163.4 },
                new List<double> { -200, 200 }, // shoulders
                new List<double> { -200, 200 },
                new List<double> { -124.0, 119.8 }, // head
                new List<double> { -133.8, 133.2 },
                new List<double> { -200.0, 200.0 }, // arms
                new List<double> { -200.0, 200.0 },
            };
            Dictionary<string, List<List<double>>> fieldGeometry = new Dictionary<string, List<List<double>>>
            {
                { "Isocenters", isocenters },
                { "Jaw_X", jawX },
                { "Jaw_Y", jawY },
            };
            Structure upperPTV = this.upperPlan.StructureSet.Structures.FirstOrDefault(s => s.Id == StructureHelper.UPPER_PTV_NO_JUNCTION);
            upperPlan.SetIsocentersUpper(Client.MODEL_NAME_ARMS, upperPTV, fieldGeometry);

            try
            {
                foreach (Beam beam in upperPlan.Beams)
                {
                    if (beam.BeamNumber == 7) // right arm iso
                    {
                        Assert.Equal(355.0, beam.ControlPoints.First().CollimatorAngle);
                        Assert.Equal(179.9, beam.ControlPoints.First().GantryAngle);
                        Assert.Equal(355.0, beam.ControlPoints.Last().GantryAngle);
                        Assert.Equal(GantryDirection.CounterClockwise, beam.GantryDirection);
                    }
                    else if (beam.BeamNumber == 8) // left arm iso
                    {
                        Assert.Equal(5, beam.ControlPoints.First().CollimatorAngle);
                        Assert.Equal(180.1, beam.ControlPoints.First().GantryAngle);
                        Assert.Equal(5, beam.ControlPoints.Last().GantryAngle);
                        Assert.Equal(GantryDirection.Clockwise, beam.GantryDirection);
                    }
                    else if (beam.BeamNumber % 2 != 0)
                    {
                        Assert.Equal(90, beam.ControlPoints.First().CollimatorAngle);
                        Assert.Equal(179.9, beam.ControlPoints.First().GantryAngle);
                        Assert.Equal(180.1, beam.ControlPoints.Last().GantryAngle);
                        Assert.Equal(GantryDirection.CounterClockwise, beam.GantryDirection);
                    }
                    else
                    {
                        Assert.Equal(90, beam.ControlPoints.First().CollimatorAngle);
                        Assert.Equal(180.1, beam.ControlPoints.First().GantryAngle);
                        Assert.Equal(179.9, beam.ControlPoints.Last().GantryAngle);
                        Assert.Equal(GantryDirection.Clockwise, beam.GantryDirection);
                    }
                }
            }
            catch (EqualException e)
            {
                throw new Exception("Unexpected upper field configuration", e);
            }
            finally
            {
                // Teardown
                upperPlan.Beams.ToList().ForEach(beam => planInContext.RemoveBeam(beam));
            }
        }
    }
}