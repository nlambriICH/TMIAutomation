using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMIAutomation.Async;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public class UpperJunction : IStructure
    {
        private readonly ILogger logger = Log.ForContext<UpperJunction>();
        private readonly EsapiWorker esapiWorker;
        private readonly string courseId;
        private readonly string upperPlanId;
        private readonly string upperPTVId;

        public UpperJunction(EsapiWorker esapiWorker, string courseId, string upperPlanId, string upperPTVId)
        {
            this.esapiWorker = esapiWorker;
            this.courseId = courseId;
            this.upperPlanId = upperPlanId;
            this.upperPTVId = upperPTVId;
        }

        public Task CreateAsync(IProgress<double> progress, IProgress<string> message)
        {
            return this.esapiWorker.RunAsync(scriptContext =>
            {
                logger.Information("UpperJunction context: {@context}", new List<string> { this.courseId, this.upperPlanId, this.upperPTVId });

                /*
                 * Create junction structures upper CT
                 */
                Course targetCourse = scriptContext.Patient.Courses.FirstOrDefault(c => c.Id == this.courseId);
                StructureSet upperSS = targetCourse.PlanSetups.FirstOrDefault(p => p.Id == this.upperPlanId).StructureSet;

                Structure upperPTV = upperSS.Structures.FirstOrDefault(s => s.Id == this.upperPTVId);

                List<Structure> junctionSubStructures = new List<Structure>
                {
                    upperSS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION25, logger),
                    upperSS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION50, logger),
                    upperSS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION75, logger),
                    upperSS.TryAddStructure("PTV", StructureHelper.PTV_JUNCTION100, logger)
                };

                progress.Report(0.30);
                message.Report("Generating Junction structures...");

                int count = 0;
                int bottomSliceUpperPTV = upperSS.GetStructureSlices(upperPTV).FirstOrDefault();

                foreach (int slice in Enumerable.Range(bottomSliceUpperPTV, 8))
                {
                    VVector[][] contours = upperPTV.GetContoursOnImagePlane(slice);
                    foreach (VVector[] contour in contours)
                    {
                        junctionSubStructures[count / 2].AddContourOnImagePlane(contour, slice);
                    }
                    count++;
                }

                logger.Information("Structures created: {@junctionSubStructures}", junctionSubStructures.Select(s => s.Id));

                Structure upperJunction = upperSS.TryAddStructure("PTV", StructureHelper.UPPER_PTV_JUNCTION, logger);
                junctionSubStructures.ForEach(s => upperJunction.SegmentVolume = upperJunction.Or(s));

                logger.Information("Structure created: {@upperJunction}", upperJunction.Id);

                Structure upperPTVNoJ = upperSS.TryAddStructure("PTV", StructureHelper.UPPER_PTV_NO_JUNCTION, logger);
                upperPTVNoJ.SegmentVolume = upperPTV.Sub(upperJunction);

                logger.Information("Structure created: {upperPTVNoJ}", upperPTVNoJ.Id);

                Structure upperLegsPTV = upperSS.Structures.FirstOrDefault(s =>
                {
                    string structureIdLower = s.Id.ToLower();
                    return s.DicomType == "PTV" && (structureIdLower.Contains("legs") || structureIdLower.Contains("gambe"));
                });

                if (upperLegsPTV != null)
                {
                    Structure upperLegsPTVNoJ = upperSS.TryAddStructure(upperLegsPTV.DicomType, StructureHelper.UPPER_PTV_LEGS, logger);
                    upperLegsPTVNoJ.SegmentVolume = upperLegsPTV.Sub(upperJunction);
                    logger.Information("Structure created: {upperLegsPTVNoJ}", upperLegsPTVNoJ.Id);
                }
                else
                {
                    logger.Warning("Structure {upperLegsPTVNoJ} could not be created. " +
                        "The script needs a PTV structure containing \"legs\" in its name (e.g., \"PTV_legs\")", StructureHelper.UPPER_PTV_LEGS);
                }

                progress.Report(0.30);
                message.Report("Generating REM_AUTO optimization structure...");

                Structure rem = upperSS.TryAddStructure("AVOIDANCE", StructureHelper.REM, logger);
                IEnumerable<int> slicesIsodose25 = upperSS.GetStructureSlices(junctionSubStructures[0]);
                int topSliceIsodose25 = slicesIsodose25.LastOrDefault();
                int bottomSliceRem = slicesIsodose25.FirstOrDefault() - 2;

                Structure body = upperSS.GetExternal(logger);
                Structure bodyShrunk = upperSS.TryAddStructure("AVOIDANCE", "tempBody", logger);
                bodyShrunk.SegmentVolume = body.Margin(-20);

                foreach (int slice in Enumerable.Range(bottomSliceRem, topSliceIsodose25 - bottomSliceRem))
                {
                    VVector[][] contours = bodyShrunk.GetContoursOnImagePlane(slice);
                    foreach (VVector[] contour in contours)
                    {
                        rem.AddContourOnImagePlane(contour, slice);
                    }
                }
                upperSS.RemoveStructure(bodyShrunk);
                rem.SegmentVolume = rem.Sub(upperJunction.AsymmetricMargin(new AxisAlignedMargins(StructureMarginGeometry.Outer, 10, 10, 0, 10, 10, 0)));

                logger.Information("Structure created: {rem}", rem.Id);
                progress.Report(0.4);
                message.Report("Done!");
            });
        }
    }
}
