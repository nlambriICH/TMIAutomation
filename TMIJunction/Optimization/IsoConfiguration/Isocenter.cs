using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIJunction
{
    class Isocenter
    {

        private readonly string lowerPlan;
        private readonly string machineName;
        private readonly Course latestCourse;
        private readonly ILogger logger;

        public Isocenter(Course latestCourse, string lowerPlan, string machineName)
        {
            this.lowerPlan = lowerPlan;
            this.machineName = machineName;
            this.latestCourse = latestCourse;
            this.logger = Log.ForContext<Isocenter>();
        }

        public void Set(ScriptContext context)
        {
            ExternalPlanSetup plan = latestCourse.ExternalPlanSetups.Where(ps => ps.Id == lowerPlan).FirstOrDefault();

            foreach (Beam beam in plan.Beams.ToList())
            {
                logger.Information("Removing existing beam: {beamID}", beam.Id);
                plan.RemoveBeam(beam);
            }
            
            StructureSet legSS = plan.StructureSet;
            Structure ptvTotal = legSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_TOTAL);

            // The Rect3D X, Y, Z are placed near the feet
            Rect3D rect = ptvTotal.MeshGeometry.Bounds;
            double isoStep = rect.SizeZ / 4;

            logger.Information("Using isocenter step [mm]: {isoStep}", isoStep);

            // Isocenter positions in CC direction
            List<VVector> isoPositions = new List<VVector>
            {
                new VVector(ptvTotal.CenterPoint.x, ptvTotal.CenterPoint.y, rect.Z + rect.SizeZ - 0.75 * isoStep),
                new VVector(ptvTotal.CenterPoint.x, ptvTotal.CenterPoint.y, rect.Z + rect.SizeZ - 2 * isoStep),
                new VVector(ptvTotal.CenterPoint.x, ptvTotal.CenterPoint.y - 30, rect.Z + rect.SizeZ - 3.35 * isoStep) // shift y for the feet
            };

            double fieldY = rect.SizeX / 2 + 15; // Y1 and Y2 jaw aperture
            double feetFieldY = rect.SizeX / 2 + 30; // Y1 and Y2 jaw aperture

            if (feetFieldY > 200) feetFieldY = 200; // maximum field aperture

            double fieldXIso12 = 1.25 * isoStep / 2 + 20; // half z-distance iso + 20 mm to obtain approx 40 mm of overlap
            double fieldXIso23 = 1.35 * isoStep / 2 + 20; // half z-distance iso + 20 mm to obtain approx 40 mm of overlap

            // Jaw positions for the isocenters. X1 towards the head
            List<Tuple<VRect<double>,VRect<double>>> jawPositions = new List<Tuple<VRect<double>, VRect<double>>>
            {
                Tuple.Create(
                    new VRect<double>(-isoStep, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -fieldY, fieldXIso12, fieldY)
                    ),
                Tuple.Create(
                    new VRect<double>(-fieldXIso12, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -fieldY, fieldXIso23, fieldY)
                    ),
                Tuple.Create(
                    new VRect<double>(-fieldXIso23, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -feetFieldY, isoStep, feetFieldY)
                    )
            };

            logger.Information("Overlap between fields same isocenter [mm]: {overlapSameIso}", 20); 
            logger.Information("Overlap between fields adjacent isocenters [mm]: {overlapAdjIso}", isoStep * .2);

            for (int i = 0; i < isoPositions.Count(); ++i)
            {

                logger.Information("Adding fields at isocenter {num}. Isocenter coordinates [mm]: {@isocenter}", i + 1, isoPositions[i]);
                
                logger.Information("Field {first}. Jaw position [mm]: {@jawPosition}", 2 * i + 1, jawPositions[i].Item1);

                plan.AddArcBeam(
                    new ExternalBeamMachineParameters(machineName, "6X", 600, "ARC", ""),
                    jawPositions[i].Item1,
                    90,
                    180.1,
                    179.9,
                    GantryDirection.Clockwise,
                    0,
                    isoPositions[i]
                );

                logger.Information("Field {second}. Jaw position [mm]: {@jawPosition}", 2 * i + 2, jawPositions[i].Item2);

                plan.AddArcBeam(
                    new ExternalBeamMachineParameters(machineName, "6X", 600, "ARC", ""),
                    jawPositions[i].Item2,
                    90,
                    179.9,
                    180.1,
                    GantryDirection.CounterClockwise,
                    0,
                    isoPositions[i]
                );
            }

        }
    }
}
