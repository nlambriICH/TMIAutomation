using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIJunction.Async;
using VMS.TPS.Common.Model.API;

namespace TMIJunction
{
    public class BodyControlStructures : IStructure
    {
        private readonly string bodyPlanId;
        private readonly string bodyPTVId;
        private readonly ILogger logger = Log.ForContext<BodyControlStructures>();
        private readonly EsapiWorker esapiWorker;
        private readonly string upperPlanId;
        private readonly string upperPTVId;

        public BodyControlStructures(string bodyPlanId, string ptvId)
        {
            this.bodyPlanId = bodyPlanId;
            this.bodyPTVId = ptvId;
            this.logger = Log.ForContext<BodyControlStructures>();
        }

        public BodyControlStructures(EsapiWorker esapiWorker, string upperPlanId, string upperPTVId)
        {
            this.esapiWorker = esapiWorker;
            this.upperPlanId = upperPlanId;
            this.upperPTVId = upperPTVId;
        }

        public Task CreateAsync(IProgress<double> progress, IProgress<string> message)
        {
            return esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("BodyControlStructures context: {@context}", new List<string> { upperPlanId, upperPTVId });

                /*
                * Create Healthy Tissue (HT), Healthy Tissue 2 (HT2), and Body Free (Body_free)
                */
                StructureSet bodySS = scriptContext.PlansInScope.FirstOrDefault(p => p.Id == upperPlanId).StructureSet;
                Structure ptv = bodySS.Structures.FirstOrDefault(s => s.Id == upperPTVId);
                int bottomSlicePTVWithJunction = bodySS.GetStructureSlices(ptv).FirstOrDefault();
                int clearBodyFreeOffset = 3;
                int bodyFreeSliceRemove = bottomSlicePTVWithJunction - clearBodyFreeOffset;

                bodySS.CreateHealthyTissue(ptv, logger, progress, message);
                logger.Information("Structures created: {healthyTissue} {healthyTissue2}", StructureHelper.HEALTHY_TISSUE, StructureHelper.HEALTHY_TISSUE2);

                bodySS.CreateBodyFree(ptv, 0, bodyFreeSliceRemove, logger, progress, message);
                logger.Information("Structure created: {bodyFree}", StructureHelper.BODY_FREE);

                progress.Report(0.25);
                message.Report("Done!");
            });
        }

        public void Create(ScriptContext context)
        {

            //logger.Information("BodyControlStructures context: {@context}", new List<string> { bodyPlanId, bodyPTVId });

            ///*
            // * Create Healthy Tissue (HT), Healthy Tissue 2 (HT2), and Body Free (Body_free)
            // */
            //StructureSet bodySS = context.PlansInScope.FirstOrDefault(p => p.Id == bodyPlanId).StructureSet;
            //Structure ptv = bodySS.Structures.FirstOrDefault(s => s.Id == bodyPTVId);
            //int bottomSlicePTVWithJunction = bodySS.GetStructureSlices(ptv).FirstOrDefault();
            //int clearBodyFreeOffset = 3;
            //int bodyFreeSliceRemove = bottomSlicePTVWithJunction - clearBodyFreeOffset;

            //bodySS.CreateHealthyTissue(ptv, logger);
            //logger.Information("Structures created: {healthyTissue} {healthyTissue2}", StructureHelper.HEALTHY_TISSUE, StructureHelper.HEALTHY_TISSUE2);

            //bodySS.CreateBodyFree(ptv, 0, bodyFreeSliceRemove, logger);
            //logger.Information("Structure created: {bodyFree}", StructureHelper.BODY_FREE);
        }
    }
}
