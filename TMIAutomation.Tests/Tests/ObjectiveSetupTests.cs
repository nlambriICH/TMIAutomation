using System;
using System.Collections.Generic;
using System.Linq;
using TMIAutomation.Tests.Attributes;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using Xunit;

namespace TMIAutomation.Tests
{
    public class ObjectiveSetupTests : TestBase
    {
        private OptimizationSetup optSetup;
        private PluginScriptContext scriptContext;

        public override ITestBase Init(object testObject, params object[] optParams)
        {
            this.optSetup = testObject as OptimizationSetup;
            this.scriptContext = optParams.OfType<PluginScriptContext>().FirstOrDefault();
            return this.scriptContext == null
                ? throw new ArgumentException($"A PluginScriptContext must be provided to instantiate {this.GetType()}")
                : this;
        }

        [Fact]
        private void ClearObjectives()
        {
            optSetup.ClearObjectives();
            Assert.Empty(optSetup.Objectives);
        }

        [Fact]
        private void AddPointObjectives_NoPrescription_Exception()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(() => optSetup.AddPointObjectives(this.scriptContext.PlanSetup.StructureSet));
            Assert.Equal("Dose prescription must be defined\nbefore starting optimization.\r\n", exception.Message, ignoreLineEndingDifferences: true);
        }

        [Fact]
        private void AddPointObjectives_StructureId()
        {
            List<string> expectedIds = new List<string> {
                "Dose_100%", "PTV_J25%", "PTV_J25%", "PTV_J50%", "PTV_J50%", "PTV_J75%", "PTV_J75%",
                "PTV_J100%", "PTV_J100%", "LowerPTVNoJ", "LowerPTVNoJ", "HT_AUTO", "HT_AUTO",
                "HT2_AUTO", "Body_Free_AUTO"
            };
            // Need to set the prescription before adding optimization objectives
            this.scriptContext.PlanSetup.SetPrescription(1, new DoseValue(1, DoseValue.DoseUnit.Gy), 1);
            optSetup.AddPointObjectives(this.scriptContext.PlanSetup.StructureSet);
            List<string> structureIds = optSetup.Objectives.OfType<OptimizationPointObjective>().Select(obj => obj.StructureId).ToList();
            Assert.Equal(expectedIds, structureIds);
        }

        [Fact]
        private void AddEUDObjectives_StructureId()
        {
            List<string> expectedIds = new List<string> {
                "Dose_100%", "HT_AUTO", "HT2_AUTO", "Body_Free_AUTO", "REM_AUTO"
            };
            // Need to set the prescription before adding optimization objectives
            this.scriptContext.PlanSetup.SetPrescription(1, new DoseValue(1, DoseValue.DoseUnit.Gy), 1);
            optSetup.AddEUDObjectives(this.scriptContext.PlanSetup.StructureSet);
            List<string> structureIds = optSetup.Objectives.OfType<OptimizationEUDObjective>().Select(obj => obj.StructureId).ToList();
            Assert.Equal(expectedIds, structureIds);
        }
    }
}
