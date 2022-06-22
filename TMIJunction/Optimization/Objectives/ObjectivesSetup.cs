using Serilog;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIJunction
{
    static public class ObjectivesSetup
    {
        private static readonly ILogger logger = Log.ForContext(typeof(ObjectivesSetup));

        public static void ClearObjectives(this OptimizationSetup optSetup)
        {
            foreach (var objective in optSetup.Objectives)
            {
                optSetup.RemoveObjective(objective);
            }
        }

        public static void AddPointObjectives(this OptimizationSetup optSetup, StructureSet ss)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (var line in File.ReadLines(Path.Combine(assemblyDir, "..", "..", "Configuration", "PointOptimizationObjectives.txt")).Skip(3))
            {
                string[] pointObjectiveParams = line.Split('\t');
                Structure structure = ss.Structures.FirstOrDefault(s => s.Id == pointObjectiveParams[0]);
                var limit = (OptimizationObjectiveOperator)Enum.Parse(typeof(OptimizationObjectiveOperator), pointObjectiveParams[1], true);
                if (double.TryParse(pointObjectiveParams[2], out double volume)
                    && double.TryParse(pointObjectiveParams[3], out double doseValue)
                    && double.TryParse(pointObjectiveParams[5], out double priority))
                {
                    string doseUnit = pointObjectiveParams[4];

                    optSetup.AddPointObjective(structure, limit, new DoseValue(doseValue, doseUnit), volume, priority);
                }
                else
                {
                    LoggerHelper.LogAndWarnException(logger, new InvalidDataException($"Fail parsing PointObjectives: {line}"));
                }
            }
        }

        public static void AddEUDObjectives(this OptimizationSetup optSetup, StructureSet ss)
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            foreach (var line in File.ReadLines(Path.Combine(assemblyDir, "..", "..", "Configuration", "EUDOptimizationObjectives.txt")).Skip(3))
            {
                string[] eudObjectiveParams = line.Split('\t');
                Structure structure = ss.Structures.FirstOrDefault(s => s.Id == eudObjectiveParams[0]);
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
                    LoggerHelper.LogAndWarnException(logger, new InvalidDataException($"Fail parsing EUDObjectives: {line}"));
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
