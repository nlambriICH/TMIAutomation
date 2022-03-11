using Serilog;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
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

            logger.Information("Create body junction context: {@context}", new List<string> { bodyPlanId, bodyPTVId });

            /*
             * Create junction structures body CT
             */
            StructureSet bodySS = context.PlansInScope.FirstOrDefault(p => p.Id == bodyPlanId).StructureSet;

            Structure ptvBodyWithJunction = bodySS.Structures.FirstOrDefault(s => s.Id == bodyPTVId);

            context.Patient.BeginModifications();

            List<Structure> junctionSubStructures = new List<Structure>
            {
                bodySS.AddStructure("PTV", StructureHelper.PTV_JUNCTION25),
                bodySS.AddStructure("PTV", StructureHelper.PTV_JUNCTION50),
                bodySS.AddStructure("PTV", StructureHelper.PTV_JUNCTION75),
                bodySS.AddStructure("PTV", StructureHelper.PTV_JUNCTION100)
            };

            int count = 0;
            int bottomSlicePTVBodyWithJunction = StructureHelper.GetStructureSlices(ptvBodyWithJunction, bodySS).FirstOrDefault();

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

            Structure bodyJunction = bodySS.AddStructure("PTV", StructureHelper.PTV_JUNCTION);
            junctionSubStructures.ForEach(s => bodyJunction.SegmentVolume = bodyJunction.Or(s));

            logger.Information("Structure created: {@bodyJunction}", bodyJunction.Id);

            Structure ptvTotNoJunctionBody = bodySS.AddStructure("PTV", StructureHelper.PTV_TOT_NO_JUNCTION);
            ptvTotNoJunctionBody.SegmentVolume = ptvBodyWithJunction.Sub(bodyJunction);

            logger.Information("Structure created: {ptvTotNoJunctionBody}", ptvTotNoJunctionBody.Id);

            Structure ptvLegsBody = bodySS.Structures.FirstOrDefault(s =>
            {
                string structureIdLower = s.Id.ToLower();
                return s.DicomType == "PTV" && (structureIdLower.Contains("legs") || structureIdLower.Contains("gambe"));
            });
            Structure ptvLegsBodyNoJunction = bodySS.AddStructure(ptvLegsBody.DicomType, StructureHelper.PTV_LEGS_NO_JUNCTION);
            ptvLegsBodyNoJunction.SegmentVolume = ptvLegsBody.Sub(bodyJunction);

            logger.Information("Structure created: {ptvLegsBodyNoJunction}", ptvLegsBodyNoJunction.Id);

            Structure rem = bodySS.AddStructure("AVOIDANCE", StructureHelper.REM);
            IEnumerable<int> slicesIsodose25 = StructureHelper.GetStructureSlices(junctionSubStructures[0], bodySS);
            int topSliceIsodose25 = slicesIsodose25.LastOrDefault();
            int bottomSliceRem = slicesIsodose25.FirstOrDefault() - 2;

            foreach (int slice in Enumerable.Range(bottomSliceRem, topSliceIsodose25 - bottomSliceRem))
            {
                Structure body = bodySS.Structures.FirstOrDefault(s => s.Id == StructureHelper.BODY);
                Structure bodyShrunk = bodySS.AddStructure("AVOIDANCE", "tempBody");
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
