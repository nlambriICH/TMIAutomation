﻿using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIAutomation
{
    static public class ObjectivesSetup
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

        public static void AddPointObjectives(this OptimizationSetup optSetup, StructureSet ss)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string pointObjPath = Path.Combine(assemblyDir, "Configuration", "PointOptimizationObjectives.txt");
            logger.Verbose("Reading PointObjectives from {pointObjPath}", pointObjPath);
            foreach (string line in File.ReadLines(pointObjPath).Skip(3))
            {
                string[] pointObjectiveParams = line.Split('\t');
                logger.Verbose("Read parameters: {@pointObjectiveParams}", pointObjectiveParams);
                Structure structure = ss.Structures.FirstOrDefault(s => s.Id == pointObjectiveParams[0]);

                if (structure == null)
                {
                    logger.Error(new InvalidOperationException($"Cannot add PointObjective for {pointObjectiveParams[0]} because is not present in the given structure set."),
                        "The following error occured during the script execution");
                    continue;
                }

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
        }

        public static void AddEUDObjectives(this OptimizationSetup optSetup, StructureSet ss)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string eudObjPath = Path.Combine(assemblyDir, "Configuration", "EUDOptimizationObjectives.txt");
            logger.Verbose("Reading EUDObjectives from {pointObjPath}", eudObjPath);
            foreach (var line in File.ReadLines(eudObjPath).Skip(3))
            {
                string[] eudObjectiveParams = line.Split('\t');
                logger.Verbose("Read parameters: {@eudObjectiveParams}", eudObjectiveParams);
                Structure structure = ss.Structures.FirstOrDefault(s => s.Id == eudObjectiveParams[0]);

                if (structure == null)
                {
                    logger.Error(new InvalidOperationException($"Cannot add EUDObjective for {eudObjectiveParams[0]} because is not present in the given structure set."),
                  "The following error occured during the script execution");
                    continue;
                }

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