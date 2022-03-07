using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    class BodyJunction : IStructure
    {
        private readonly string bodyPlanId;
        private readonly string ptvId;

        public BodyJunction(string bodyPlanId, string ptvId)
        {
            this.bodyPlanId = bodyPlanId;
            this.ptvId = ptvId;
        }

        public void Create(ScriptContext context)
        {
            /*
             * Create junction structures body CT
             */
            StructureSet bodySS = context.PlansInScope.FirstOrDefault(p => p.Id == bodyPlanId).StructureSet;

            Structure ptvBodyWithJunction = bodySS.Structures.FirstOrDefault(s => s.Id == ptvId);

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

            Structure bodyJunction = bodySS.AddStructure("PTV", StructureHelper.PTV_JUNCTION);
            junctionSubStructures.ForEach(s => bodyJunction.SegmentVolume = bodyJunction.Or(s));

            Structure ptvTotNoJunctionBody = bodySS.AddStructure("PTV", StructureHelper.PTV_TOT_NO_JUNCTION);
            ptvTotNoJunctionBody.SegmentVolume = ptvBodyWithJunction.Sub(bodyJunction);

        }
    }
}
