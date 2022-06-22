using Serilog;
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
        private readonly ILogger logger;
        private static readonly int numIsos = 3;

        public Isocenter(string lowerPlan, string machineName)
        {
            this.lowerPlan = lowerPlan;
            this.machineName = machineName;
            this.logger = Log.ForContext<Isocenter>();
        }

        public void Set(ScriptContext context)
        {
            ExternalPlanSetup plan = context.Course.ExternalPlanSetups.Where(ps => ps.Id == lowerPlan).FirstOrDefault();

            context.Patient.BeginModifications();

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

            List<double> zIsoPositions = new List<double> { };

            for (int i = 1; i <= numIsos; ++i)
            {
                zIsoPositions.Add(rect.Z + i * isoStep);
            }

            logger.Information("Using isocenter positions [mm]: {@zIsoPositions}", zIsoPositions);

            double fieldY = rect.SizeX / 2 + 30; // Y1 and Y2 jaw aperture

            // X1 towards head
            List<VRect<double>> jawPositions = new List<VRect<double>>
            {
                new VRect<double>(-isoStep * 0.5, -fieldY, isoStep + 40, fieldY), // +40 cm in X2 to include the feet in BEV
                new VRect<double>(-isoStep * 0.5, -fieldY, isoStep * 0.7, fieldY),
                new VRect<double>(-isoStep - 40, -fieldY, isoStep * .7, fieldY) // -40 cm in X1 to include the femurs in BEV
            };

            logger.Information("Using jaw positions [mm]: {@jawPositions}", jawPositions);
            logger.Information("Overlap between fields [mm]: {overlap}", isoStep * .2);

            for (int i = 0; i < zIsoPositions.Count(); ++i)
            {
                plan.AddArcBeam(
                    new ExternalBeamMachineParameters(machineName, "6X", 600, "ARC", ""),
                    jawPositions[i],
                    90,
                    180.1,
                    179.9,
                    GantryDirection.Clockwise,
                    0,
                    new VVector(ptvTotal.CenterPoint.x, ptvTotal.CenterPoint.y, zIsoPositions[i])
                );

                plan.AddArcBeam(
                    new ExternalBeamMachineParameters(machineName, "6X", 600, "ARC", ""),
                    jawPositions[i],
                    90,
                    179.9,
                    180.1,
                    GantryDirection.CounterClockwise,
                    0,
                    new VVector(ptvTotal.CenterPoint.x, ptvTotal.CenterPoint.y, zIsoPositions[i])
                );
            }

        }
    }
}
