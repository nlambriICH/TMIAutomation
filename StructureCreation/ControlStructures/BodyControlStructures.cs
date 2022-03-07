using System.Linq;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation
{
    class BodyControlStructures : IStructure
    {
        private readonly string bodyPlanId;
        private readonly string ptvId;

        public BodyControlStructures(string bodyPlanId, string ptvId)
        {
            this.bodyPlanId = bodyPlanId;
            this.ptvId = ptvId;
        }

        public void Create(ScriptContext context)
        {

            context.Patient.BeginModifications();

            /*
             * Create Healthy Tissue (HT), Healthy Tissue 2 (HT2), and Body Free (Body_free)
             */
            StructureSet bodySS = context.PlansInScope.FirstOrDefault(p => p.Id == bodyPlanId).StructureSet;
            Structure ptv = bodySS.Structures.FirstOrDefault(s => s.Id == ptvId);
            int bottomSlicePTVWithJunction = StructureHelper.GetStructureSlices(ptv, bodySS).FirstOrDefault();
            int clearBodyFreeOffset = 3;
            int bodyFreeSliceRemove = bottomSlicePTVWithJunction - clearBodyFreeOffset;
            StructureHelper.CreateHealthyTissue(bodySS, ptv);
            StructureHelper.CreateBodyFree(bodySS, ptv, 1, bodyFreeSliceRemove);
        }
    }
}
