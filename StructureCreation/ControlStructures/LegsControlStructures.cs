using System;
using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    class LegsControlStructures : IStructure
    {
        private readonly string legsPlanId;
        private readonly string ptvId;

        public LegsControlStructures(string legsPlanId, string ptvId)
        {
            this.legsPlanId = legsPlanId;
            this.ptvId = ptvId;
        }

        public void Create(ScriptContext context)
        {

            context.Patient.BeginModifications();

            /*
             * Create Healthy Tissue (HT), Healthy Tissue 2 (HT2), and Body Free (Body_free)
             */
            StructureSet legsSS = context.PlansInScope.FirstOrDefault(p => p.Id == legsPlanId).StructureSet;
            Structure ptv = legsSS.Structures.FirstOrDefault(s => s.Id == ptvId);
            int bottomSlicePTVWithJunction = StructureHelper.GetStructureSlices(ptv, legsSS).LastOrDefault();
            int clearBodyFreeOffset = 3;
            int bodyFreeSliceStart = bottomSlicePTVWithJunction + clearBodyFreeOffset;
            int bodyFreeSliceRemove = legsSS.Image.ZSize - bottomSlicePTVWithJunction - clearBodyFreeOffset;
            StructureHelper.CreateHealthyTissue(legsSS, ptv);
            StructureHelper.CreateBodyFree(legsSS, ptv, bodyFreeSliceStart, bodyFreeSliceRemove);
        }
    }
}
