﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Serilog;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    public static class ObjectivesSetup
    {
        private static readonly ILogger logger = Log.ForContext(typeof(ObjectivesSetup));

        public static void ClearObjectives(this OptimizationSetup optSetup)
        {
            foreach (OptimizationObjective objective in optSetup.Objectives)
            {
                logger.Verbose("Removing existing obective: {objective}", objective);
                optSetup.RemoveObjective(objective);
            }
        }

        public static void AddPointObjectives(this OptimizationSetup optSetup,
                                              StructureSet ss)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string pointObjPath = Path.Combine(assemblyDir, "Configuration", "PointOptimizationObjectives.txt");
            logger.Verbose("Reading PointObjectives from {pointObjPath}", pointObjPath);

            foreach (string line in File.ReadLines(pointObjPath).Skip(4))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;

                string[] pointObjectiveParams = line.Split('\t');
                logger.Verbose("Read parameters: {@pointObjectiveParams}", pointObjectiveParams);
                Structure structure = ss.Structures.FirstOrDefault(s => s.Id == pointObjectiveParams[0]);

                if (structure == null)
                {
                    logger.Error(new InvalidOperationException($"Cannot add PointObjective for {pointObjectiveParams[0]} because is not present in the given structure set."),
                                 "The following error occured during the script execution");
                    continue;
                }
#if ESAPI15
                List<string> skipIds = new List<string> {
                    StructureHelper.DOSE_100,
                    StructureHelper.PTV_JUNCTION25,
                    StructureHelper.PTV_JUNCTION50,
                    StructureHelper.PTV_JUNCTION75,
                    StructureHelper.PTV_JUNCTION100,
                };

                if (ConfigOptOptions.BaseDosePlanning && skipIds.Contains(structure.Id))
                {
                    logger.Information("Base-dose planning is selected. Skip PointObjective for {structureId}", structure.Id);
                    continue;
                }
#endif

                OptimizationObjectiveOperator limit = (OptimizationObjectiveOperator)Enum.Parse(typeof(OptimizationObjectiveOperator), pointObjectiveParams[1], true);
                if (double.TryParse(pointObjectiveParams[2], out double volume)
                    && double.TryParse(pointObjectiveParams[3], out double doseValue)
                    && double.TryParse(pointObjectiveParams[5], out double priority))
                {
                    string doseUnit = pointObjectiveParams[4];
                    optSetup.AddPointObjective(structure, limit, new DoseValue(doseValue, doseUnit), volume, priority);
                }
                else
                {
                    logger.Error(new InvalidDataException($"Fail parsing PointObjectives: {line}"), "The following error occured during the script execution");
                }
            }
#if ESAPI15
            // Add point objectives for junction in case user has not defined any with base-dose planning
            if (ConfigOptOptions.BaseDosePlanning && !optSetup.Objectives.OfType<OptimizationPointObjective>()
                                                                         .Any(obj => obj.StructureId == StructureHelper.LOWER_PTV_JUNCTION))
            {
                Structure structure = ss.Structures.FirstOrDefault(s => s.Id == StructureHelper.LOWER_PTV_JUNCTION);
                optSetup.AddPointObjective(structure, OptimizationObjectiveOperator.Upper, new DoseValue(2.1, "Gy"), 0, 150);
                optSetup.AddPointObjective(structure, OptimizationObjectiveOperator.Lower, new DoseValue(2.0, "Gy"), 100, 200);
                logger.Warning("Add PoinObjectives not specified by the user for {junction}", StructureHelper.LOWER_PTV_JUNCTION);
            }
#endif
        }

        public static void AddEUDObjectives(this OptimizationSetup optSetup, StructureSet ss)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string eudObjPath = Path.Combine(assemblyDir, "Configuration", "EUDOptimizationObjectives.txt");
            logger.Verbose("Reading EUDObjectives from {pointObjPath}", eudObjPath);
            foreach (string line in File.ReadLines(eudObjPath).Skip(4))
            {
                if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;

                string[] eudObjectiveParams = line.Split('\t');
                logger.Verbose("Read parameters: {@eudObjectiveParams}", eudObjectiveParams);
                Structure structure = ss.Structures.FirstOrDefault(s => s.Id == eudObjectiveParams[0]);

                if (structure == null)
                {
                    logger.Error(new InvalidOperationException($"Cannot add EUDObjective for {eudObjectiveParams[0]} because is not present in the given structure set."),
                                 "The following error occured during the script execution");
                    continue;
                }
#if ESAPI15
                if (ConfigOptOptions.BaseDosePlanning && structure.Id == StructureHelper.REM)
                {
                    logger.Information("Base-dose planning is selected. Skip EUDObjective for {structureId}", structure.Id);
                    continue;
                }
#endif

                var limit = (OptimizationObjectiveOperator)Enum.Parse(typeof(OptimizationObjectiveOperator), eudObjectiveParams[1], true);
                if (double.TryParse(eudObjectiveParams[2], out double doseValue)
                    && double.TryParse(eudObjectiveParams[4], out double priority)
                    && double.TryParse(eudObjectiveParams[5], out double gEUDa))
                {
                    string doseUnit = eudObjectiveParams[3];
                    optSetup.AddEUDObjective(structure, limit, new DoseValue(doseValue, doseUnit), gEUDa, priority);
                }
                else
                {
                    logger.Error(new InvalidDataException($"Fail parsing EUDObjectives: {line}"), "The following error occured during the script execution");
                }
            }
#if ESAPI15
            // Add EUD objective to Dose_100% if upper objective is not >= 2 Gy with base-dose planning
            if (ConfigOptOptions.BaseDosePlanning && !optSetup.Objectives.OfType<OptimizationEUDObjective>()
                                                                         .Any(obj => obj.StructureId == StructureHelper.DOSE_100
                                                                             && obj.Operator == OptimizationObjectiveOperator.Upper
                                                                             && obj.Dose >= new DoseValue(2, "Gy")))
            {
                foreach (OptimizationEUDObjective objective in optSetup.Objectives.OfType<OptimizationEUDObjective>()
                                                                                  .Where(obj => obj.StructureId == StructureHelper.DOSE_100
                                                                                      && obj.Operator == OptimizationObjectiveOperator.Upper))
                {
                    optSetup.RemoveObjective(objective);
                }
                Structure structure = ss.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_100);
                optSetup.AddEUDObjective(structure, OptimizationObjectiveOperator.Upper, new DoseValue(2.1, "Gy"), 40, 70);
                logger.Warning("Add EUDObjectives not specified by the user for {dose}", StructureHelper.DOSE_100);
            }
#endif
        }

        public static void AddPointObjectivesAdditionalOptCycle(this OptimizationSetup optSetup,
                                                                StructureSet ss,
                                                                DoseValue totalDose,
                                                                OptimizationCycleTarget optCycleTarget)
        {
            switch (optCycleTarget)
            {
                case OptimizationCycleTarget.LowerPTVNoJ:
                    optSetup.AddPointObjective(ss.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_105), OptimizationObjectiveOperator.Upper, 1.1 * totalDose, 100, 100);
                    optSetup.AddPointObjective(ss.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_95), OptimizationObjectiveOperator.Lower, totalDose, 100, 150);
                    break;
                case OptimizationCycleTarget.LowerPTV_J:
                    optSetup.AddPointObjective(ss.Structures.FirstOrDefault(s => s.Id == StructureHelper.DOSE_100_PS), OptimizationObjectiveOperator.Lower, 1.1 * totalDose, 100, 150);
                    break;
                default:
                    break;
            }
        }

        public static void ExcludeStructuresFromOptimization(this OptimizationSetup optSetup, StructureSet ss)
        {
            foreach (var optParams in optSetup.Parameters)
            {
                //var optPar = optParams as OptimizationExcludeStructureParameter;
                //if (optPar != null)
                //{
                //    var s = optPar.Structure;
                //}
                //optSetup.RemoveParameter(optParams as OptimizationExcludeStructureParameter);
            }
        }
    }

}