using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIJunction.Async;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIJunction
{
    public class BodyJunction : IStructure
    {
        private readonly string bodyPlanId;
        private readonly string bodyPTVId;
        private readonly ILogger logger = Log.ForContext<BodyJunction>();
        private readonly EsapiWorker esapiWorker;
        private readonly string upperPlanId;
        private readonly string upperPTVId;

        public BodyJunction(string bodyPlanId, string ptvId)
        {
            this.bodyPlanId = bodyPlanId;
            this.bodyPTVId = ptvId;
            this.logger = Log.ForContext<BodyJunction>();
        }

        public BodyJunction(EsapiWorker esapiWorker, string upperPlanId, string upperPTVId)
        {
            this.esapiWorker = esapiWorker;
            this.upperPlanId = upperPlanId;
            this.upperPTVId = upperPTVId;
        }

        public Task CreateAsync(IProgress<double> progress, IProgress<string> message)
        {
            return esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("BodyJunction context: {@context}", new List<string> { upperPlanId, upperPTVId });

                message.Report("Starting execution...");
                /*
                 * Create junction structures body CT
                 */
                StructureSet bodySS = scriptContext.PlansInScope.FirstOrDefault(p => p.Id == upperPlanId).StructureSet;

                Structure ptvBodyWithJunction = bodySS.Structures.FirstOrDefault(s => s.Id == upperPTVId);

                List<Structure> junctionSubStructures = new List<Structure>
                {
                    bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION25, logger),
                    bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION50, logger),
                    bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION75, logger),
                    bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION100, logger)
                };

                progress.Report(0.25);
                message.Report("Creating Junction...");

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

                Structure bodyJunction = bodySS.TryAddStructure("PTV", StructureHelper.UPPER_PTV_JUNCTION, logger);
                junctionSubStructures.ForEach(s => bodyJunction.SegmentVolume = bodyJunction.Or(s));

                logger.Information("Structure created: {@bodyJunction}", bodyJunction.Id);

                Structure ptvTotNoJunctionBody = bodySS.TryAddStructure("PTV", StructureHelper.UPPER_PTV_NO_JUNCTION, logger);
                ptvTotNoJunctionBody.SegmentVolume = ptvBodyWithJunction.Sub(bodyJunction);

                logger.Information("Structure created: {ptvTotNoJunctionBody}", ptvTotNoJunctionBody.Id);

                progress.Report(0.5);

                Structure ptvLegsBody = bodySS.Structures.FirstOrDefault(s =>
                {
                    string structureIdLower = s.Id.ToLower();
                    return s.DicomType == "PTV" && (structureIdLower.Contains("legs") || structureIdLower.Contains("gambe"));
                });

                if (ptvLegsBody != null)
                {
                    Structure ptvLegsBodyNoJunction = bodySS.TryAddStructure(ptvLegsBody.DicomType, StructureHelper.UPPER_PTV_LEGS, logger);
                    ptvLegsBodyNoJunction.SegmentVolume = ptvLegsBody.Sub(bodyJunction);
                    logger.Information("Structure created: {ptvLegsBodyNoJunction}", ptvLegsBodyNoJunction.Id);
                }
                else
                {
                    logger.Warning("Structure {ptvLegsBodyNoJunction} could not be created. " +
                        "The script needs a PTV structure containing \"legs\" in its name (e.g., \"PTV_legs\")", StructureHelper.UPPER_PTV_LEGS);
                }

                progress.Report(0.75);
                message.Report("Creating REM...");

                Structure rem = bodySS.TryAddStructure("AVOIDANCE", StructureHelper.REM, logger);
                IEnumerable<int> slicesIsodose25 = bodySS.GetStructureSlices(junctionSubStructures[0]);
                int topSliceIsodose25 = slicesIsodose25.LastOrDefault();
                int bottomSliceRem = slicesIsodose25.FirstOrDefault() - 2;

                Structure body = bodySS.Structures.FirstOrDefault(s => s.Id == StructureHelper.BODY);
                Structure bodyShrunk = bodySS.TryAddStructure("AVOIDANCE", "tempBody", logger);
                bodyShrunk.SegmentVolume = body.Margin(-20);

                foreach (int slice in Enumerable.Range(bottomSliceRem, topSliceIsodose25 - bottomSliceRem))
                {
                    VVector[][] contours = bodyShrunk.GetContoursOnImagePlane(slice);
                    foreach (VVector[] contour in contours)
                    {
                        rem.AddContourOnImagePlane(contour, slice);
                    }
                }
                bodySS.RemoveStructure(bodyShrunk);
                rem.SegmentVolume = rem.Sub(bodyJunction.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 10, 10, 0, 10, 10, 0)));

                progress.Report(1);

                logger.Information("Structure created: {rem}", rem.Id);
            });
        }

        public void Create(ScriptContext context)
        {
            //logger.Information("BodyJunction context: {@context}", new List<string> { bodyPlanId, bodyPTVId });

            ///*
            // * Create junction structures body CT
            // */
            //StructureSet bodySS = context.PlansInScope.FirstOrDefault(p => p.Id == bodyPlanId).StructureSet;

            //Structure ptvBodyWithJunction = bodySS.Structures.FirstOrDefault(s => s.Id == bodyPTVId);

            //List<Structure> junctionSubStructures = new List<Structure>
            //{
            //    bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION25, logger),
            //    bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION50, logger),
            //    bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION75, logger),
            //    bodySS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION100, logger)
            //};

            //int count = 0;
            //int bottomSlicePTVBodyWithJunction = bodySS.GetStructureSlices(ptvBodyWithJunction).FirstOrDefault();

            //foreach (int slice in Enumerable.Range(bottomSlicePTVBodyWithJunction, 8))
            //{
            //    VVector[][] contours = ptvBodyWithJunction.GetContoursOnImagePlane(slice);
            //    foreach (VVector[] contour in contours)
            //    {
            //        junctionSubStructures[count / 2].AddContourOnImagePlane(contour, slice);
            //    }
            //    count++;
            //}

            //logger.Information("Structures created: {@junctionSubStructures}", junctionSubStructures.Select(s => s.Id));

            //Structure bodyJunction = bodySS.TryAddStructure("PTV", StructureHelper.UPPER_PTV_JUNCTION, logger);
            //junctionSubStructures.ForEach(s => bodyJunction.SegmentVolume = bodyJunction.Or(s));

            //logger.Information("Structure created: {@bodyJunction}", bodyJunction.Id);

            //Structure ptvTotNoJunctionBody = bodySS.TryAddStructure("PTV", StructureHelper.UPPER_PTV_NO_JUNCTION, logger);
            //ptvTotNoJunctionBody.SegmentVolume = ptvBodyWithJunction.Sub(bodyJunction);

            //logger.Information("Structure created: {ptvTotNoJunctionBody}", ptvTotNoJunctionBody.Id);

            //Structure ptvLegsBody = bodySS.Structures.FirstOrDefault(s =>
            //{
            //    string structureIdLower = s.Id.ToLower();
            //    return s.DicomType == "PTV" && (structureIdLower.Contains("legs") || structureIdLower.Contains("gambe"));
            //});

            //if (ptvLegsBody != null)
            //{
            //    Structure ptvLegsBodyNoJunction = bodySS.TryAddStructure(ptvLegsBody.DicomType, StructureHelper.UPPER_PTV_LEGS, logger);
            //    ptvLegsBodyNoJunction.SegmentVolume = ptvLegsBody.Sub(bodyJunction);
            //    logger.Information("Structure created: {ptvLegsBodyNoJunction}", ptvLegsBodyNoJunction.Id);
            //}
            //else
            //{
            //    logger.Warning("Structure {ptvLegsBodyNoJunction} could not be created. " +
            //        "The script needs a PTV structure containing \"legs\" in its name (e.g., \"PTV_legs\")", StructureHelper.UPPER_PTV_LEGS);
            //}

            //Structure rem = bodySS.TryAddStructure("AVOIDANCE", StructureHelper.REM, logger);
            //IEnumerable<int> slicesIsodose25 = bodySS.GetStructureSlices(junctionSubStructures[0]);
            //int topSliceIsodose25 = slicesIsodose25.LastOrDefault();
            //int bottomSliceRem = slicesIsodose25.FirstOrDefault() - 2;

            //foreach (int slice in Enumerable.Range(bottomSliceRem, topSliceIsodose25 - bottomSliceRem))
            //{
            //    Structure body = bodySS.Structures.FirstOrDefault(s => s.Id == StructureHelper.BODY);
            //    Structure bodyShrunk = bodySS.TryAddStructure("AVOIDANCE", "tempBody", logger);
            //    bodyShrunk.SegmentVolume = body.Margin(-20);
            //    VVector[][] contours = bodyShrunk.GetContoursOnImagePlane(slice);
            //    foreach (VVector[] contour in contours)
            //    {
            //        rem.AddContourOnImagePlane(contour, slice);
            //    }
            //    bodySS.RemoveStructure(bodyShrunk);
            //}
            //rem.SegmentVolume = rem.Sub(bodyJunction.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 10, 10, 0, 10, 10, 0)));

            //logger.Information("Structure created: {rem}", rem.Id);

        }
    }
}
