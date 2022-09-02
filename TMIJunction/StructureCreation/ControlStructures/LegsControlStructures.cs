using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIJunction.Async;
using VMS.TPS.Common.Model.API;

namespace TMIJunction
{
    public class LegsControlStructures : IStructure
    {
        private readonly PlanSetup legsPlan;
        private readonly string legsPTVId;
        private readonly ILogger logger = Log.ForContext<LegsControlStructures>();
        private readonly EsapiWorker esapiWorker;

        public LegsControlStructures(EsapiWorker esapiWorker)
        {
            this.esapiWorker = esapiWorker;
        }

        public Task CreateAsync(IProgress<double> progress, IProgress<string> message)
        {
            return esapiWorker.RunAsync(scriptContext =>
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
            });
        }
    }
}
