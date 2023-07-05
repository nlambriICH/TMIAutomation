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
        [MemberData(nameof(TryAddStructureRename_Data))]
        private void TryAddStructureRename(string dicomType, string id, string expectedId)
        {
            Structure newStructure = structureSet.TryAddStructure(dicomType, id, this.logger);
            Assert.Equal(expectedId, newStructure.Id);
        }

        public static IEnumerable<object[]> TryAddStructureRename_Data()
        {
            yield return new object[] { "CONTROL", "TestStructure", "TestStructure" };
            yield return new object[] { "CONTROL", "Dose_25%", "Dose_25%" };
            yield return new object[] { "CONTROL", "Dose_50%", "Dose_50%" };
        }

        [Theory]
        [MemberData(nameof(TryAddStructureRemove_Data))]
        private void TryAddStructureRemove(string dicomType, string id, string expectedId)
        {
            Structure newStructure = structureSet.TryAddStructure(dicomType, id, this.logger);
            Assert.Equal(expectedId, newStructure.Id);
        }

        public static IEnumerable<object[]> TryAddStructureRemove_Data()
        {
            yield return new object[] { "CONTROL", "Dose_25%", "Dose_25%" };
            yield return new object[] { "CONTROL", "Dose_50%", "Dose_50%" };
            yield return new object[] { "CONTROL", "Dose_75%", "Dose_75%" };
        }

#if ESAPI15
        [Fact]
        private void TryAddStructureRename_Approved_Exception()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => structureSet.TryAddStructure("CONTROL", "Dose_75%", this.logger));
            Assert.Equal("Could not change Id of the existing Structure Dose_75%. Please set its status to UnApproved in all StructureSets.",
                         exception.Message,
                         ignoreLineEndingDifferences: true);
        }
#endif

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