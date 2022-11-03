using System;
using System.Linq;
using TMIAutomation.Tests.Attributes;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Xunit;

namespace TMIAutomation.Tests
{
    public class CalculationTests : TestBase
    {
        private ExternalPlanSetup externalPlanSetup;
        private PluginScriptContext scriptContext;
        public override ITestBase Init(object testObject, params object[] optParams)
        {
            this.externalPlanSetup = testObject as ExternalPlanSetup;
            this.scriptContext = optParams.OfType<PluginScriptContext>().FirstOrDefault();
            return this.scriptContext == null
                ? throw new ArgumentException($"A PluginScriptContext must be provided to instantiate {this.GetType()}")
                : this;
        }

        [Fact]
        private void OptimizationSetup_Algorithm()
        {
            string optionName = "/PhotonOptCalculationOptions/@MRLevelAtRestart";
            string expectedOptAlgo = "PO_15.6.06";
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

            Assert.Equal(expectedOptAlgo, optAlgo);
            Assert.Equal(expectedDoseAlgo, doseAlgo);
            Assert.Equal(expectedOptValue, optionValue);
            Assert.Equal(expectedDoseValue, dosePerFraction.ValueAsString);
            Assert.Equal(expectedDoseUnit, dosePerFraction.UnitAsString);
            Assert.Equal(expectedNumFractions, numFractions);
        }
    }
}
