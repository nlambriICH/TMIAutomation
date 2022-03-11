using Serilog;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation
{
    class BodyControlStructures : IStructure
    {
        private readonly string bodyPlanId;
        private readonly string bodyPTVId;
        private readonly ILogger logger;

        public BodyControlStructures(string bodyPlanId, string ptvId)
        {
            this.bodyPlanId = bodyPlanId;
            this.bodyPTVId = ptvId;
            this.logger = Log.ForContext<BodyControlStructures>();
        }

        public void Create(ScriptContext context)
        {

            logger.Information("Create body control structures context: {@context}", new List<string> { bodyPlanId, bodyPTVId });

            context.Patient.BeginModifications();

            /*
             * Create Healthy Tissue (HT), Healthy Tissue 2 (HT2), and Body Free (Body_free)
             */
            StructureSet bodySS = context.PlansInScope.FirstOrDefault(p => p.Id == bodyPlanId).StructureSet;
            Structure ptv = bodySS.Structures.FirstOrDefault(s => s.Id == bodyPTVId);
            int bottomSlicePTVWithJunction = StructureHelper.GetStructureSlices(ptv, bodySS).FirstOrDefault();
            int clearBodyFreeOffset = 3;
            int bodyFreeSliceRemove = bottomSlicePTVWithJunction - clearBodyFreeOffset;
            
            StructureHelper.CreateHealthyTissue(bodySS, ptv);
            logger.Information("Structures created: {healthyTissue} {healthyTissue2}", StructureHelper.HEALTHY_TISSUE, StructureHelper.HEALTHY_TISSUE2);

            StructureHelper.CreateBodyFree(bodySS, ptv, 0, bodyFreeSliceRemove);
            logger.Information("Structure created: {bodyFree}", StructureHelper.BODY_FREE);
        }
    }
}
