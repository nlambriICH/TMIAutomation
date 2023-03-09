using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class StructureHelper
    {
        public const string PTV_JUNCTION25 = "PTV_J25%";
        public const string PTV_JUNCTION50 = "PTV_J50%";
        public const string PTV_JUNCTION75 = "PTV_J75%";
        public const string PTV_JUNCTION100 = "PTV_J100%";
        public const string UPPER_PTV_LEGS = "UpperLegsPTVNoJ";
        public const string DOSE_25 = "Dose_25%";
        public const string DOSE_50 = "Dose_50%";
        public const string DOSE_75 = "Dose_75%";
        public const string DOSE_100 = "Dose_100%";
        public const string UPPER_PTV_JUNCTION = "UpperPTV_J";
        public const string LOWER_PTV_JUNCTION = "LowerPTV_J";
        public const string UPPER_PTV_NO_JUNCTION = "UpperPTVNoJ";
        public const string LOWER_PTV_NO_JUNCTION = "LowerPTVNoJ";
        public const string PTV_TOTAL = "PTV_Total";
        public const string REM = "REM_AUTO";
        public const string HEALTHY_TISSUE = "HT_AUTO";
        public const string HEALTHY_TISSUE2 = "HT2_AUTO";
        public const string BODY_FREE = "Body_Free_AUTO";
        public const string DOSE_105 = "Dose_105%";
        public const string DOSE_95 = "Dose_95%";
        public const string DOSE_100_PS = "Dose_100%_PS";

        // BODY : ECLIPSE default name for patient outline
        // External : RayStation default name for patient outline
        private static readonly List<string> externals = new List<string> { "BODY", "EXTERNAL" };

        public static Structure CreateStructureFromIsodose(this StructureSet ss,
                                                           string junctionId,
                                                           Structure ptvWithJunction,
                                                           Structure isodose,
                                                           ILogger logger)
        {
            IEnumerable<int> juncSlices = ss.GetStructureSlices(ptvWithJunction);
            IEnumerable<int> isoSlices = ss.GetStructureSlices(isodose);
            Structure junction = ss.TryAddStructure("PTV", junctionId, logger);
            ss.AddJunctionContoursFromIsodose(ptvWithJunction, juncSlices, isoSlices, isodose, junction);

            return junction;
        }

        public static void AddJunctionContoursFromIsodose(this StructureSet ss,
                                                          Structure wholeJunction,
                                                          IEnumerable<int> juncSlices,
                                                          IEnumerable<int> isoSlices,
                                                          Structure isodose,
                                                          Structure subJunction)
        {
            foreach (int slice in juncSlices.Intersect(isoSlices))
            {
                if (!ss.IsEmptyContourIntersection(wholeJunction, isodose, slice))
                {
                    foreach (VVector[] contour in wholeJunction.GetContoursOnImagePlane(slice))
                    {
                        subJunction.AddContourOnImagePlane(contour, slice);
                    }
                }
            }
        }

        public static bool IsEmptyContourIntersection(this StructureSet ss,
                                                      Structure structure,
                                                      Structure other,
                                                      int slice)
        {
            Structure ptvSlice = ss.AddStructure("PTV", "tempPTV");
            foreach (VVector[] contour in structure.GetContoursOnImagePlane(slice))
            {
                ptvSlice.AddContourOnImagePlane(contour, slice);
            }

            Structure isodoseSlice = ss.AddStructure("CONTROL", "tempIsodose");
            foreach (VVector[] contour in other.GetContoursOnImagePlane(slice))
            {
                isodoseSlice.AddContourOnImagePlane(contour, slice);
            }

            Structure intersection = ss.AddStructure("CONTROL", "tempIntersection");
            intersection.SegmentVolume = ptvSlice.And(isodoseSlice);
            bool isEmptyIntersection = intersection.IsEmpty;

            if (!isEmptyIntersection && intersection.Volume < ptvSlice.Volume * 0.10)
            {
                isEmptyIntersection = true;
            }

            ss.RemoveStructure(ptvSlice);
            ss.RemoveStructure(isodoseSlice);
            ss.RemoveStructure(intersection);

            return isEmptyIntersection;
        }

        public static IEnumerable<int> GetStructureSlices(this StructureSet ss, Structure structure)
        {
            Rect3D rect = structure.MeshGeometry.Bounds;
            int firstSlice = ss.GetSlice(rect.Z);
            int lastSlice = ss.GetSlice(rect.Z + rect.SizeZ);
            return Enumerable.Range(firstSlice, lastSlice - firstSlice + 1);
        }

        public static int GetSlice(this StructureSet ss, double z)
        {
            double imageRes = ss.Image.ZRes;
            return Convert.ToInt32((z - ss.Image.Origin.z) / imageRes);
        }

        public static void CreateHealthyTissue(this StructureSet ss,
                                               Structure ptv,
                                               ILogger logger,
                                               IProgress<double> progress,
                                               IProgress<string> message)
        {
            Structure body = ss.GetExternal(logger);

            progress.Report(0.25);
            message.Report("Generating healthy tissue structure HT_AUTO...");
            Structure healthyTissue = ss.TryAddStructure("CONTROL", HEALTHY_TISSUE, logger);
            healthyTissue.SegmentVolume = ptv.Margin(15).Sub(ptv.Margin(3)).And(body.Margin(-3));

            message.Report("Removing small contours from HT_AUTO. This may take a while...");
            logger.Information("RemoveSmallContoursFromStructure: {HT}", HEALTHY_TISSUE);
            ss.RemoveSmallContoursFromStructure(healthyTissue, message);

            progress.Report(0.25);
            message.Report("Generating healthy tissue structure HT2_AUTO...");
            Structure healthyTissue2 = ss.TryAddStructure("CONTROL", HEALTHY_TISSUE2, logger);
            healthyTissue2.SegmentVolume = ptv.Margin(30).Sub(ptv.Margin(17)).And(body.Margin(-3));

            message.Report("Removing small contours from HT2_AUTO. This may take a while...");
            logger.Information("RemoveSmallContoursFromStructure: {HT2}", HEALTHY_TISSUE2);
            ss.RemoveSmallContoursFromStructure(healthyTissue2, message);
        }

        public static void CreateBodyFree(this StructureSet ss,
                                          Structure ptv,
                                          int bodyFreeSliceStart,
                                          int bodyFreeSliceRemove,
                                          ILogger logger,
                                          IProgress<double> progress,
                                          IProgress<string> message)
        {
            Structure body = ss.GetExternal(logger);

            progress.Report(0.25);
            message.Report("Generating healthy tissue structure Body_Free_AUTO...");

            Structure bodyFree = ss.TryAddStructure("CONTROL", BODY_FREE, logger);
            bodyFree.SegmentVolume = body.Margin(-3).Sub(ptv.Margin(35));

            foreach (int slice in Enumerable.Range(bodyFreeSliceStart, bodyFreeSliceRemove))
            {
                bodyFree.ClearAllContoursOnImagePlane(slice);
            }

            message.Report("Removing small contours from Body_Free_AUTO. This may take a while...");
            logger.Information("RemoveSmallContoursFromStructure: {BodyFree}", BODY_FREE);
            ss.RemoveSmallContoursFromStructure(bodyFree, message);
        }

        public static void CreateIsodoseOptStructures(this StructureSet ss,
                                                      PlanningItem planningItem,
                                                      Structure targetVolume,
                                                      OptimizationCycleTarget optCycleTarget,
                                                      ILogger logger,
                                                      IProgress<string> message)
        {
            switch (optCycleTarget)
            {
                case OptimizationCycleTarget.LowerPTVNoJ:
                    message.Report("Generating isodose Dose_105%...");
                    Structure isodose105 = ss.TryAddStructure("CONTROL", DOSE_105, logger);
                    planningItem.DoseValuePresentation = DoseValuePresentation.Relative;
                    isodose105.ConvertDoseLevelToStructure(planningItem.Dose, new DoseValue(105.0, DoseValue.DoseUnit.Percent));
                    isodose105.SegmentVolume = targetVolume.Sub(isodose105);
                    logger.Information("RemoveSmallContoursFromStructure: {Dose_105}", DOSE_105);
                    ss.RemoveSmallContoursFromStructure(isodose105, message);

                    message.Report("Generating isodose Dose_95%...");
                    Structure isodose95 = ss.TryAddStructure("CONTROL", DOSE_95, logger);
                    isodose95.ConvertDoseLevelToStructure(planningItem.Dose, new DoseValue(95.0, DoseValue.DoseUnit.Percent));
                    isodose95.SegmentVolume = targetVolume.Sub(isodose95);
                    logger.Information("RemoveSmallContoursFromStructure: {Dose_95}", DOSE_95);
                    ss.RemoveSmallContoursFromStructure(isodose95, message);

                    break;
                case OptimizationCycleTarget.LowerPTV_J:
                    message.Report("Generating isodose Dose_100%_PS...");
                    Structure isodose100PlanSum = ss.TryAddStructure("CONTROL", DOSE_100_PS, logger);
                    isodose100PlanSum.ConvertDoseLevelToStructure(planningItem.Dose, new DoseValue(2.0, DoseValue.DoseUnit.Gy));
                    isodose100PlanSum.SegmentVolume = targetVolume.Sub(isodose100PlanSum);
                    logger.Information("RemoveSmallContoursFromStructure: {Dose_100_PS}", DOSE_100_PS);
                    ss.RemoveSmallContoursFromStructure(isodose100PlanSum, message);

                    break;
                default:
                    break;
            }
        }

        private static void RemoveSmallContoursFromStructure(this StructureSet ss, Structure structure, IProgress<string> message)
        {
            List<int> structureSlices = ss.GetStructureSlices(structure).ToList();
            foreach (int slice in structureSlices)
            {
                message.Report($"Removing small contours from {structure.Id}. Slice: {structureSlices.IndexOf(slice) + 1}/{structureSlices.Count}");
                foreach (VVector[] contour in structure.GetContoursOnImagePlane(slice))
                {
                    Structure removeSmall = ss.AddStructure("CONTROL", "tempRemoveSmall");
                    removeSmall.AddContourOnImagePlane(contour, slice);
                    if (removeSmall.Volume < 0.5 * (ss.Image.ZRes / 10))
                    {
                        structure.SubtractContourOnImagePlane(contour, slice);
                    }
                    ss.RemoveStructure(removeSmall);
                }
            }
        }

        public static Structure TryAddStructure(this StructureSet ss, string dicomType, string id, ILogger logger)
        {
            if (!ss.CanAddStructure(dicomType, id))
            {
                logger.Information("Found existing Sructure {Id} in current StructureSet {ssId}", id, ss.Id);
#if ESAPI16
                string newId = id.Length >= 63 ? id.Remove(id.Length - 2, 2) + "_0" : id + "_0";
#else
                string newId = id.Length >= 15 ? id.Remove(id.Length - 2, 2) + "_0" : id + "_0";
#endif
                if (!ss.CanAddStructure(dicomType, newId))
                {
                    logger.Warning("Found existing Sructure {newId} in current StructureSet {ssId}", newId, ss.Id);
                    logger.Warning("Assuming Sructure {newId} was generated by the script", newId);
                    ss.RemoveStructure(ss.Structures.FirstOrDefault(s => s.Id == newId));
                    logger.Warning("Removed Sructure {newId}", newId);
                }
                logger.Information("Renaming existing Structure {Id} to {newId}", id, newId);
                Structure oldStructure = ss.Structures.FirstOrDefault(s => s.Id == id);
                oldStructure.Id = newId;
            }

            logger.Information("Add new structure Structure {Id}", id);
            return ss.AddStructure(dicomType, id);
        }

        public static Structure GetExternal(this StructureSet ss, ILogger logger)
        {
            Structure body = ss.Structures.FirstOrDefault(s => s.DicomType == "EXTERNAL")
                ?? ss.Structures.FirstOrDefault(s => externals.Contains(s.Id.ToUpper()));
            logger.Information("Found structure EXTERNAL: {body}", body);

            return body;
        }

    }
}
