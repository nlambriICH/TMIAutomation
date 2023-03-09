using System;
using System.Collections.Generic;
using System.Linq;
using Serilog;
using TMIAutomation.Tests.Attributes;
using VMS.TPS.Common.Model.API;
using Xunit;

namespace TMIAutomation.Tests
{
    public class StructureHelperTests : TestBase
    {
        private StructureSet structureSet;
        private readonly ILogger logger = Log.ForContext<StructureHelperTests>();

        public override ITestBase Init(object testObject, params object[] optParams)
        {
            this.structureSet = testObject as StructureSet;
            return this;
        }

        [Theory]
        [MemberData(nameof(TryAddStructure_Data))]
        private void TryAddStructure(string dicomType, string id, string expectedId, string expectedExistingStructureNewId)
        {
            Structure newStructure = structureSet.TryAddStructure(dicomType, id, this.logger);
            Assert.Equal(expectedId, newStructure.Id);
            Assert.Contains(expectedExistingStructureNewId, structureSet.Structures.Select(s => s.Id));
        }

        public static IEnumerable<object[]> TryAddStructure_Data()
        {
            yield return new object[] { "CONTROL", "TestStructure", "TestStructure", "TestStructure" };
            yield return new object[] { "CONTROL", "Dose_25%", "Dose_25%", "Dose_25%_0" };
            yield return new object[] { "CONTROL", "Dose_50%", "Dose_50%", "Dose_50%_0" };
#if ESAPI16
            yield return new object[] {
                "CONTROL",
                "LongStructureName64Characters_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx01",
                "LongStructureName64Characters_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx01",
                "LongStructureName64Characters_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx_0"
            };
#else
            yield return new object[] { "CONTROL", "16CharactersName", "16CharactersName", "16CharactersNa_0" };
#endif
        }

        [Fact]
        private void TryAddStructure_Approved_Exception()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => structureSet.TryAddStructure("CONTROL", "Dose_75%", this.logger));
            Assert.Equal("Could not change Id of the existing Structure Dose_75%. Please check its status is UnApproved in all StructureSets.",
                         exception.Message,
                         ignoreLineEndingDifferences: true);
        }

        [Theory]
        [InlineData(new object[] { "LowerPTV_J", 132, 140 })] // slice count starts from feet (FFS)
        private void GetStructureSlices(string structureId, int firstSlice, int lastSlice)
        {
            Structure structure = structureSet.Structures.FirstOrDefault(s => s.Id == structureId);
            List<int> slices = structureSet.GetStructureSlices(structure).ToList();
            Assert.Equal(firstSlice, slices.First());
            Assert.Equal(lastSlice, slices.Last());
        }

        [Fact]
        private void GetExternal()
        {
            Structure structure = structureSet.GetExternal(this.logger);
            Assert.Equal("EXTERNAL", structure.DicomType);
            Assert.Equal("BODY", structure.Id);
        }
    }
}
