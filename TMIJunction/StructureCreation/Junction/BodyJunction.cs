using Serilog;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIJunction
{
    class BodyJunction : IStructure
    {
        private readonly string bodyPlanId;
        private readonly string bodyPTVId;
        private readonly ILogger logger;

        public BodyJunction(string bodyPlanId, string ptvId)
        {
            this.bodyPlanId = bodyPlanId;
            this.bodyPTVId = ptvId;
            this.logger = Log.ForContext<BodyJunction>();
        }

        public void Create(ScriptContext context)
        {
            logger.Information("BodyJunction context: {@context}", new List<string> { bodyPlanId, bodyPTVId });

            /*
             * Create junction structures body CT
             */
            StructureSet bodySS = context.PlansInScope.FirstOrDefault(p => p.Id == bodyPlanId).StructureSet;

            Structure ptvBodyWithJunction = bodySS.Structures.FirstOrDefault(s => s.Id == bodyPTVId);

            context.Patient.BeginModifications();

            List<Structure> junctionSubStructures = new List<Structure>
            {
                bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION25, logger),
                bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION50, logger),
                bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION75, logger),
                bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION100, logger)
            };

            int count = 0;
            int bottomSlicePTVBodyWithJunction = bodySS.GetStructureSlices(ptvBodyWithJunction).FirstOrDefault();

            foreach (int slice in Enumerable.Range(bottomSlicePTVBodyWithJunction, 8))
            {
                VVector[][] contours = ptvBodyWithJunction.GetContoursOnImagePlane(slice);
                foreach (VVector[] contour in contours)
                {
                    junctionSubStructures[count / 2].AddContourOnImagePlane(contour, slice);
                }
                count++;
            }

            logger.Information("Structures created: {@junctionSubStructures}", junctionSubStructures.Select(s => s.Id));

            Structure bodyJunction = bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION, logger);
            junctionSubStructures.ForEach(s => bodyJunction.SegmentVolume = bodyJunction.Or(s));

            logger.Information("Structure created: {@bodyJunction}", bodyJunction.Id);

            Structure ptvTotNoJunctionBody = bodySS.TryAddStructure("PTV", StructureHelper.PTV_TOT_NO_JUNCTION, logger);
            ptvTotNoJunctionBody.SegmentVolume = ptvBodyWithJunction.Sub(bodyJunction);

            logger.Information("Structure created: {ptvTotNoJunctionBody}", ptvTotNoJunctionBody.Id);

            Structure ptvLegsBody = bodySS.Structures.FirstOrDefault(s =>
            {
                string structureIdLower = s.Id.ToLower();
                return s.DicomType == "PTV" && (structureIdLower.Contains("legs") || structureIdLower.Contains("gambe"));
            });
            Structure ptvLegsBodyNoJunction = bodySS.TryAddStructure(ptvLegsBody.DicomType, StructureHelper.PTV_LEGS_NO_JUNCTION, logger);
            ptvLegsBodyNoJunction.SegmentVolume = ptvLegsBody.Sub(bodyJunction);

            logger.Information("Structure created: {ptvLegsBodyNoJunction}", ptvLegsBodyNoJunction.Id);

            Structure rem = bodySS.TryAddStructure("AVOIDANCE", StructureHelper.REM, logger);
            IEnumerable<int> slicesIsodose25 = bodySS.GetStructureSlices(junctionSubStructures[0]);
            int topSliceIsodose25 = slicesIsodose25.LastOrDefault();
            int bottomSliceRem = slicesIsodose25.FirstOrDefault() - 2;

            foreach (int slice in Enumerable.Range(bottomSliceRem, topSliceIsodose25 - bottomSliceRem))
            {
                Structure body = bodySS.Structures.FirstOrDefault(s => s.Id == StructureHelper.BODY);
                Structure bodyShrunk = bodySS.TryAddStructure("AVOIDANCE", "tempBody", logger);
                bodyShrunk.SegmentVolume = body.Margin(-20);
                VVector[][] contours = bodyShrunk.GetContoursOnImagePlane(slice);
                foreach (VVector[] contour in contours)
                {
                    rem.AddContourOnImagePlane(contour, slice);
                }
                bodySS.RemoveStructure(bodyShrunk);
            }
            rem.SegmentVolume = rem.Sub(bodyJunction.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 10, 10, 0, 10, 10, 0)));

            logger.Information("Structure created: {rem}", rem.Id);

        }
    }
}
