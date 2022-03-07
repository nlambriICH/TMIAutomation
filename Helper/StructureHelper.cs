using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{

    static class StructureHelper
    {
        public const string PTV_JUNCTION25 = "PTV_Junction25%";
        public const string PTV_JUNCTION50 = "PTV_Junction50%";
        public const string PTV_JUNCTION75 = "PTV_Junction75%";
        public const string PTV_JUNCTION100 = "PTV_Junction100%";
        public const string DOSE_25 = "Dose_25%";
        public const string DOSE_50 = "Dose_50%";
        public const string DOSE_75 = "Dose_75%";
        public const string DOSE_100 = "Dose_100%";
        public const string PTV_JUNCTION = "PTV_Junction";
        public const string PTV_TOT_NO_JUNCTION = "PTVTotNoJunction";
        public const string PTV_TOTAL = "PTV_Total";
        public const string BODY = "BODY";
        public const string REM = "REM_AUTO";
        public const string HEALTHY_TISSUE = "HT_AUTO";
        public const string HEALTHY_TISSUE2 = "HT2_AUTO";
        public const string BODY_FREE = "Body_Free_AUTO";

        public static Structure CreateStructureFromIsodose(string junctionId, StructureSet ss, Structure ptvWithJunction, Structure isodose)
        {
            IEnumerable<int> juncSlices = GetStructureSlices(ptvWithJunction, ss);
            IEnumerable<int> isoSlices = GetStructureSlices(isodose, ss);
            Structure junction = ss.AddStructure("PTV", junctionId);
            AddJunctionContoursFromIsodose(ss, ptvWithJunction, juncSlices, isoSlices, isodose, junction);

            return junction;
        }

        public static void AddJunctionContoursFromIsodose(StructureSet ss, Structure wholeJunction,
            IEnumerable<int> juncSlices, IEnumerable<int> isoSlices, Structure isodose, Structure subJunction)
        {
            foreach (int slice in juncSlices.Intersect(isoSlices))
            {
                if (!IsEmptyContourIntersection(ss, wholeJunction, isodose, slice))
                {
                    foreach (VVector[] contour in wholeJunction.GetContoursOnImagePlane(slice))
                    {
                        subJunction.AddContourOnImagePlane(contour, slice);
                    }
                }
            }
        }

        public static bool IsEmptyContourIntersection(StructureSet ss, Structure structure, Structure other, int slice)
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

        public static IEnumerable<int> GetStructureSlices(Structure structure, StructureSet ss)
        {
            Rect3D rect = structure.MeshGeometry.Bounds;
            int firstSlice = GetSlice(rect.Z, ss);
            int lastSlice = GetSlice(rect.Z + rect.SizeZ, ss);
            return Enumerable.Range(firstSlice, lastSlice - firstSlice + 1);
        }

        public static int GetSlice(double z, StructureSet ss)
        {
            double imageRes = ss.Image.ZRes;
            return Convert.ToInt32((z - ss.Image.Origin.z) / imageRes);
        }

        public static void CreateHealthyTissue(StructureSet ss, Structure ptv)
        {
            Structure body = ss.Structures.FirstOrDefault(s => s.Id == BODY);
            Structure healthyTissue = ss.AddStructure("CONTROL", HEALTHY_TISSUE);
            Structure healthyTissue2 = ss.AddStructure("CONTROL", HEALTHY_TISSUE2);

            healthyTissue.SegmentVolume = ptv.Margin(15).Sub(ptv.Margin(3)).And(body.Margin(-3));
            healthyTissue2.SegmentVolume = ptv.Margin(30).Sub(ptv.Margin(17)).And(body.Margin(-3));
        }

        public static void CreateBodyFree(StructureSet ss, Structure ptv, int bodyFreeSliceStart, int bodyFreeSliceRemove)
        {
            Structure body = ss.Structures.FirstOrDefault(s => s.Id == BODY);
            Structure bodyFree = ss.AddStructure("CONTROL", BODY_FREE);
            
            bodyFree.SegmentVolume = body.Margin(-3).Sub(ptv.Margin(35));

            foreach (int slice in Enumerable.Range(bodyFreeSliceStart, bodyFreeSliceRemove))
            {
                bodyFree.ClearAllContoursOnImagePlane(slice);
            }
        }
    }
}
