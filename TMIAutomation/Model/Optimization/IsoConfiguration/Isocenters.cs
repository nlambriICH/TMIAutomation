using System.Collections.Generic;
using System.Linq;
using Serilog;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class Isocenters
    {
        private static readonly ILogger logger = Log.ForContext(typeof(Isocenters));

        public static void LogNewBeamInfo(this ExternalPlanSetup targetPlan, VVector isocenter, VRect<double> jawPositions)
        {
            logger.Information("Adding field {num} to {targetPlanId}. Isocenter coordinates [mm]: {@isocenter}; Jaw position [mm]: {@jawPosition}",
                               targetPlan.Beams.Count() + 1,
                               targetPlan.Id,
                               isocenter,
                               jawPositions);
        }

        public static void ClearBeams(this ExternalPlanSetup targetPlan)
        {
            foreach (Beam beam in targetPlan.Beams.ToList()) // avoid Collection modified exception
            {
                logger.Information("Removing existing beam: {beamID}", beam.Id);
                targetPlan.RemoveBeam(beam);
            }
        }

        public static void CopyCaudalIsocenter(this ExternalPlanSetup targetPlan,
                                               ExternalPlanSetup sourcePlan,
                                               Registration registration)
        {
            List<Beam> sourcePlanBeams = sourcePlan.Beams.Where(b => !b.IsSetupField).ToList();
            double minIsoPos = sourcePlanBeams.First().IsocenterPosition.z;
            foreach (Beam beam in sourcePlanBeams.Skip(1))
            {
                if (beam.IsocenterPosition.z < minIsoPos) minIsoPos = beam.IsocenterPosition.z;
            }

            IEnumerable<Beam> beamsCaudalIso = sourcePlanBeams.Where(beam => beam.IsocenterPosition.z == minIsoPos);

            foreach (Beam beam in beamsCaudalIso)
            {
                VVector beamIso = beam.IsocenterPosition;
                VVector transformedIso = sourcePlan.StructureSet.Image.FOR == registration.SourceFOR
                    ? registration.TransformPoint(beamIso)
                    : registration.InverseTransformPoint(beamIso);

                BeamParameters beamParams = beam.GetEditableParameters();
                List<ControlPointParameters> cpParams = beamParams.ControlPoints.ToList();

                double collAngle = cpParams.First().CollimatorAngle;
                double transformedCollAngle = collAngle > 180 ? collAngle - 180 : 180 + collAngle;
                double transformedGantryAngle = cpParams.Last().GantryAngle;
                double transformedGantryStop = cpParams.First().GantryAngle;

                Beam newBeam = targetPlan.AddVMATBeam(
                    new ExternalBeamMachineParameters(beam.TreatmentUnit.Id, beam.EnergyModeDisplayName, beam.DoseRate, beam.Technique.Id, ""),
                    cpParams.Select(cp => cp.MetersetWeight),
                    transformedCollAngle,
                    transformedGantryAngle,
                    transformedGantryStop,
                    beamParams.GantryDirection == GantryDirection.Clockwise ? GantryDirection.CounterClockwise : GantryDirection.Clockwise,
                    beam.ControlPoints.First().PatientSupportAngle,
                    transformedIso
                );

                newBeam.Id = beam.Id;

                BeamParameters newBeamParams = newBeam.GetEditableParameters();
                /* ISSUE: CalculateDoseWithPresetValues does not work for VMAT
                 * set expected MUs with weight factor
                 */
                newBeamParams.WeightFactor = beam.WeightFactor;
                List<ControlPointParameters> newBeamCPParams = newBeamParams.ControlPoints.ToList();
                foreach (ControlPointParameters cpUpperBeam in cpParams)
                {
                    newBeamCPParams.ElementAt(cpUpperBeam.Index).JawPositions = cpUpperBeam.JawPositions;
                    newBeamCPParams.ElementAt(cpUpperBeam.Index).LeafPositions = cpUpperBeam.LeafPositions;
#if ESAPI18
                    newBeamCPParams.ElementAt(cpParams.Count() - 1 - cpUpperBeam.Index).GantryAngle = cpUpperBeam.GantryAngle;
#endif
                }
                newBeam.ApplyParameters(newBeamParams);
                logger.Information("Caudal field {beam} copied to lower dose-base plan", newBeam.Id);
            }
        }

#if ESAPI18
        public static void CopyBeam(this ExternalPlanSetup targetPlan, Beam beam)
        {
            VVector beamIso = beam.IsocenterPosition;
            BeamParameters beamParams = beam.GetEditableParameters();
            List<ControlPointParameters> cpParams = beamParams.ControlPoints.ToList();

            Beam newBeam = targetPlan.AddVMATBeam(
                new ExternalBeamMachineParameters(beam.TreatmentUnit.Id, beam.EnergyModeDisplayName, beam.DoseRate, beam.Technique.Id, ""),
                cpParams.Select(cp => cp.MetersetWeight),
                cpParams.First().CollimatorAngle,
                cpParams.First().GantryAngle,
                cpParams.Last().GantryAngle,
                beamParams.GantryDirection,
                beam.ControlPoints.First().PatientSupportAngle,
                beamIso
            );

            newBeam.Id = beam.Id;

            BeamParameters newBeamParams = newBeam.GetEditableParameters();
            /* ISSUE: CalculateDoseWithPresetValues does not work for VMAT
             * set expected MUs with weight factor
             */
            newBeamParams.WeightFactor = beam.WeightFactor;
            List<ControlPointParameters> newBeamCPParams = newBeamParams.ControlPoints.ToList();
            foreach (ControlPointParameters cpUpperBeam in cpParams)
            {
                newBeamCPParams.ElementAt(cpUpperBeam.Index).JawPositions = cpUpperBeam.JawPositions;
                newBeamCPParams.ElementAt(cpUpperBeam.Index).LeafPositions = cpUpperBeam.LeafPositions;
                newBeamCPParams.ElementAt(cpUpperBeam.Index).GantryAngle = cpUpperBeam.GantryAngle;
            }
            newBeam.ApplyParameters(newBeamParams);
            logger.Information("Field {beam} copied to {newPlanId}", newBeam.Id, targetPlan.Id);
        }
#endif
    }
}