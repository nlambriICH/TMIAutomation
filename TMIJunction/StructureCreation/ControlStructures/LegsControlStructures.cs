using Serilog;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace TMIJunction
{
    class LegsControlStructures : IStructure
    {
        private readonly PlanSetup legsPlan;
        private readonly string legsPTVId;
        private readonly ILogger logger;

        public LegsControlStructures(PlanSetup legsPlan, string ptvId)
        {
            this.legsPlan = legsPlan;
            this.legsPTVId = ptvId;
            this.logger = Log.ForContext<LegsControlStructures>();
        }

        public void Create(ScriptContext context)
        {

            logger.Information("LegsControlStructures context: {@context}", new List<string> { legsPlan.Id, legsPTVId });

            /*
             * Create Healthy Tissue (HT), Healthy Tissue 2 (HT2), and Body Free (Body_free)
             */
            StructureSet legsSS = legsPlan.StructureSet;
            Structure ptv = legsSS.Structures.FirstOrDefault(s => s.Id == legsPTVId);
            int bottomSlicePTVWithJunction = legsSS.GetStructureSlices(ptv).LastOrDefault();
            int clearBodyFreeOffset = 3;
            int bodyFreeSliceStart = bottomSlicePTVWithJunction + clearBodyFreeOffset;
            int bodyFreeSliceRemove = legsSS.Image.ZSize - bottomSlicePTVWithJunction - clearBodyFreeOffset;

            legsSS.CreateHealthyTissue(ptv, logger);
            logger.Information("Structures created: {healthyTissue} {healthyTissue2}", StructureHelper.HEALTHY_TISSUE, StructureHelper.HEALTHY_TISSUE2);

            legsSS.CreateBodyFree(ptv, bodyFreeSliceStart, bodyFreeSliceRemove, logger);
            logger.Information("Structure created: {bodyFree}", StructureHelper.BODY_FREE);
        }
    }
}
