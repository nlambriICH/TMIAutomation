﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Media3D;
using Serilog;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class Isocenters
    {
        private static readonly ILogger logger = Log.ForContext(typeof(Isocenters));

        public static void SetIsocentersUpper(this ExternalPlanSetup targetPlan, Dictionary<string, List<List<double>>> fieldGeometry)
        {
            targetPlan.ClearBeams();

            StructureSet upperSS = targetPlan.StructureSet;
            Structure upperPTVNoJ = upperSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.UPPER_PTV_NO_JUNCTION);
            double isoCoordY = upperPTVNoJ.CenterPoint.y;

            List<List<double>> isocenters = fieldGeometry["Isocenters"];
            List<List<double>> jawX = fieldGeometry["Jaw_X"];
            List<List<double>> jawY = fieldGeometry["Jaw_Y"];

            ExternalBeamMachineParameters sourcePlanBeamParams = new ExternalBeamMachineParameters(Configuration.TreatmentMachine,
                                                                                                   "6X",
                                                                                                   600,
                                                                                                   "ARC",
                                                                                                   "");

            for (int i = isocenters.Count() - 3; i >= 0; i = i - 2) // -3 because of body model
            {
                int firstIsoInGroup = i - 1;
                VVector isocenter = new VVector(isocenters[firstIsoInGroup][0], isoCoordY, isocenters[firstIsoInGroup][2]);
                VRect<double> jawPositions = new VRect<double>(jawX[firstIsoInGroup][0], jawY[firstIsoInGroup][0], jawX[firstIsoInGroup][1], jawY[firstIsoInGroup][1]);

                logger.Information("Adding field {num}. Isocenter coordinates [mm]: {@isocenter}; Jaw position [mm]: {@jawPosition}",
                                   isocenters.Count() - 2 - i,
                                   isocenter,
                                   jawPositions);

                targetPlan.AddArcBeam(
                    sourcePlanBeamParams,
                    jawPositions,
                    90,
                    179.9,
                    180.1,
                    GantryDirection.CounterClockwise,
                    0,
                    isocenter
                );

                int secondIsoInGroup = i;
                isocenter = new VVector(isocenters[secondIsoInGroup][0], isoCoordY, isocenters[secondIsoInGroup][2]);
                jawPositions = new VRect<double>(jawX[secondIsoInGroup][0], jawY[secondIsoInGroup][0], jawX[secondIsoInGroup][1], jawY[secondIsoInGroup][1]);

                logger.Information("Adding field {num}. Isocenter coordinates [mm]: {@isocenter}; Jaw position [mm]: {@jawPosition}",
                                   isocenters.Count() - 1 - i,
                                   isocenter,
                                   jawPositions);

                targetPlan.AddArcBeam(
                    sourcePlanBeamParams,
                    jawPositions,
                    90,
                    180.1,
                    179.9,
                    GantryDirection.Clockwise,
                    0,
                    isocenter
                );
            }
        }

        public static void SetIsocentersLower(this ExternalPlanSetup targetPlan, ExternalPlanSetup sourcePlan)
        {
            targetPlan.ClearBeams();

            StructureSet lowerSS = targetPlan.StructureSet;
            Structure lowerPTVTotal = lowerSS.Structures.FirstOrDefault(s => s.Id == StructureHelper.PTV_TOTAL);
            DefineIsocentersAndJaws(lowerPTVTotal,
                                    out List<VVector> isoPositions,
                                    out List<Tuple<VRect<double>, VRect<double>>> jawPositions);

            Beam beamSourcePlan = sourcePlan.Beams.FirstOrDefault(b => !b.IsSetupField);
            ExternalBeamMachineParameters sourcePlanBeamParams = new ExternalBeamMachineParameters(beamSourcePlan.TreatmentUnit.Id,
                                                                                                   beamSourcePlan.EnergyModeDisplayName,
                                                                                                   beamSourcePlan.DoseRate,
                                                                                                   beamSourcePlan.Technique.Id,
                                                                                                   "");
            logger.Information("Using beam machine parameters: {@sourcePlanBeamParams} retrieved from plan {sourcePlan}", sourcePlanBeamParams, sourcePlan.Id);

            for (int i = 0; i < isoPositions.Count(); ++i)
            {
                logger.Information("Adding fields at isocenter {num}. Isocenter coordinates [mm]: {@isocenter}", i + 1, isoPositions[i]);

                logger.Information("Field {first}. Jaw position [mm]: {@jawPosition}", (2 * i) + 1, jawPositions[i].Item1);

                targetPlan.AddArcBeam(
                    sourcePlanBeamParams,
                    jawPositions[i].Item1,
                    90,
                    180.1,
                    179.9,
                    GantryDirection.Clockwise,
                    0,
                    isoPositions[i]
                );

                logger.Information("Field {second}. Jaw position [mm]: {@jawPosition}", (2 * i) + 2, jawPositions[i].Item2);

                targetPlan.AddArcBeam(
                    sourcePlanBeamParams,
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

        private static void ClearBeams(this ExternalPlanSetup targetPlan)
        {
            foreach (Beam beam in targetPlan.Beams.ToList()) // avoid Collection modified exception
            {
                logger.Information("Removing existing beam: {beamID}", beam.Id);
                targetPlan.RemoveBeam(beam);
            }
        }

        private static void DefineIsocentersAndJaws(Structure lowerPTVTotal,
                                                    out List<VVector> isoPositions,
                                                    out List<Tuple<VRect<double>, VRect<double>>> jawPositions)
        {
            // The Rect3D X, Y, Z are placed near the feet
            Rect3D rect = lowerPTVTotal.MeshGeometry.Bounds;
            double isoStep = rect.SizeZ / 4;
            logger.Information("Using isocenter step [mm]: {isoStep}", Math.Round(isoStep, MidpointRounding.AwayFromZero));

            // Isocenter positions in CC direction
            isoPositions = new List<VVector>
            {
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y, rect.Z + rect.SizeZ - (0.75 * isoStep)),
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y, rect.Z + rect.SizeZ - (2 * isoStep)),
                new VVector(lowerPTVTotal.CenterPoint.x, lowerPTVTotal.CenterPoint.y - 30, rect.Z + rect.SizeZ - (3.35 * isoStep)) // shift y for the feet
            };
            double fieldY = (rect.SizeX / 2) + 15; // Y1 and Y2 jaw aperture
            double feetFieldY = (rect.SizeX / 2) + 30; // Y1 and Y2 jaw aperture

            if (feetFieldY > 200)
            {
                logger.Information("Reducing feet Y-field aperture [mm] from {feetFieldY} to 200", Math.Round(feetFieldY, MidpointRounding.AwayFromZero));
                feetFieldY = 200; // maximum field aperture
            }

            double fieldXIso12 = (1.25 * isoStep / 2) + 20; // half z-distance iso + 20 mm to obtain approx 40 mm of overlap
            double fieldXIso23 = (1.35 * isoStep / 2) + 20; // half z-distance iso + 20 mm to obtain approx 40 mm of overlap

            // Ensure most cranial and caudal fields have X<=200
            double field1X1 = isoStep > 200 ? 200 : isoStep;
            double field3X2 = isoStep > 200 ? 200 : isoStep;

            // Jaw positions for the isocenters. X1 towards the head
            jawPositions = new List<Tuple<VRect<double>, VRect<double>>>
            {
                Tuple.Create(
                    new VRect<double>(-field1X1, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -fieldY, fieldXIso12, fieldY)
                    ),
                Tuple.Create(
                    new VRect<double>(-fieldXIso12, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -fieldY, fieldXIso23, fieldY)
                    ),
                Tuple.Create(
                    new VRect<double>(-fieldXIso23, -fieldY, 10, fieldY),
                    new VRect<double>(-10, -feetFieldY, field3X2, feetFieldY)
                    )
            };

            logger.Information("Overlap between fields same isocenter [mm]: {overlapSameIso}", 20);
            logger.Information("Overlap between fields adjacent isocenters [mm]: {overlapAdjIso}", Math.Round(isoStep * .2, MidpointRounding.AwayFromZero));
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
                    0,
                    transformedIso
                );

                newBeam.Id = beam.Id;

                BeamParameters newBeamParams = newBeam.GetEditableParameters();
                /* ISSUE: CalculateDoseWithPresetValues does not work for VMAT
                 * set expected MUs with weight factor
                 */
                newBeamParams.WeightFactor = beam.WeightFactor;
                VRect<double> jawPos = cpParams.First().JawPositions; // assuming no jaw tracking
                newBeamParams.SetJawPositions(jawPos);
                List<ControlPointParameters> newBeamCPParams = newBeamParams.ControlPoints.ToList();
                foreach (ControlPointParameters cpUpperBeam in cpParams)
                {
                    newBeamCPParams.FirstOrDefault(cpNewBeam => cpNewBeam.Index == cpUpperBeam.Index).LeafPositions = cpUpperBeam.LeafPositions;
                }
                newBeam.ApplyParameters(newBeamParams);
                logger.Information("Caudal field {beam} copied to lower dose-base plan", newBeam.Id);
            }
        }
    }
}