using System;
using TMIAutomation.Tests.Attributes;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Xunit;
using Xunit.Sdk;

namespace TMIAutomation.Tests
{
    public class CalculationTests : TestBase
    {
        private ExternalPlanSetup externalPlanSetup;

        public override ITestBase Init(object testObject, params object[] optParams)
        {
            this.externalPlanSetup = testObject as ExternalPlanSetup;
            return this;
        }

        [Theory]
        [InlineData("LowerBase")]
        [InlineData("LowerBase")]
        private void GetOrCreateBaseDosePlan(string expectedPlanId)
        {
            Course targetCourse = externalPlanSetup.Course;
            ExternalPlanSetup newPlan = targetCourse.GetOrCreateBaseDosePlan(externalPlanSetup.StructureSet);
            try
            {
                Assert.Equal(expectedPlanId, newPlan.Id);
            }
            catch (EqualException e)
            {
                throw new Exception($"Input parameters: {expectedPlanId}", e);
            }
        }

        [Fact]
        private void OptimizationSetup_Algorithm()
        {
#if ESAPI16
            string optionName = "/PhotonOptimizerCalculationOptions/VMAT/@MRLevelAtRestart";
            string expectedOptAlgo = "PO 16.1.0";
            string expectedTargetVolumeId = StructureHelper.LOWER_PTV_NO_JUNCTION;
#else
            string optionName = "/PhotonOptCalculationOptions/@MRLevelAtRestart";
            string expectedOptAlgo = "PO_15.6.06";
#endif
            string expectedDoseAlgo = "AAA 15.06.06";
            string expectedOptValue = "MR3";
            string expectedDoseValue = "2.000";
            string expectedDoseUnit = "Gy";
            int expectedNumFractions = 1;

            this.externalPlanSetup.SetupOptimization();
            string optAlgo = this.externalPlanSetup.GetCalculationModel(CalculationType.PhotonVMATOptimization);
            string doseAlgo = this.externalPlanSetup.GetCalculationModel(CalculationType.PhotonVolumeDose);
            this.externalPlanSetup.GetCalculationOption(expectedOptAlgo, optionName, out string optionValue);
            DoseValue dosePerFraction = this.externalPlanSetup.DosePerFraction;
            int? numFractions = this.externalPlanSetup.NumberOfFractions;
#if ESAPI16
            string targetVolumeId = this.externalPlanSetup.TargetVolumeID;
#endif

            Assert.Equal(expectedOptAlgo, optAlgo);
            Assert.Equal(expectedDoseAlgo, doseAlgo);
            Assert.Equal(expectedOptValue, optionValue);
            Assert.Equal(expectedDoseValue, dosePerFraction.ValueAsString);
            Assert.Equal(expectedDoseUnit, dosePerFraction.UnitAsString);
            Assert.Equal(expectedNumFractions, numFractions);
#if ESAPI16
            Assert.Equal(expectedTargetVolumeId, targetVolumeId);
#endif
        }
    }
}