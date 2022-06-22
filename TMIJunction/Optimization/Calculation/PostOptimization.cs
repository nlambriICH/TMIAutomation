using Serilog;
using System;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;

namespace TMIJunction
{
    static class PostOptimization
    {
        private static readonly ILogger logger = Log.ForContext(typeof(PostOptimization));
        
        public static void AdjustYJawToMLCShape(this ExternalPlanSetup externalPlanSetup)
        {
            foreach (Beam beam in externalPlanSetup.Beams)
            {
                logger.Information("FitYJawToMLC for beam {beamId}", beam.Id);
                double minLeafGap = beam.MLC.MinDoseDynamicLeafGap;

                int numLeafClosedY1 = 60;
                int numLeafClosedY2 = 60;
                foreach (ControlPoint cp in beam.ControlPoints)
                {
                    VRect<double> jawPositionsCP = cp.JawPositions;
                    float[,] leafPositions = cp.LeafPositions;

                    int numLeafClosedY1CP = 0;
                    for (int i = 0; i < leafPositions.GetLength(1); ++i)
                    {
                        if (Math.Abs(leafPositions[0, i] - leafPositions[1, i]) < minLeafGap)
                        {
                            ++numLeafClosedY1CP;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (numLeafClosedY1CP < numLeafClosedY1) numLeafClosedY1 = numLeafClosedY1CP;

                    int numLeafClosedY2CP = 0;
                    for (int i = leafPositions.GetLength(1) - 1; i >= 0; --i)
                    {
                        if (Math.Abs(leafPositions[0, i] - leafPositions[1, i]) < minLeafGap)
                        {
                            ++numLeafClosedY2CP;
                        }
                        else
                        {
                            break;
                        }
                    }
                    if (numLeafClosedY2CP < numLeafClosedY2) numLeafClosedY2 = numLeafClosedY2CP;
                }

                BeamParameters beamParameters = beam.GetEditableParameters();
                double maxJawY = 200;
                foreach (ControlPointParameters cpParams in beamParameters.ControlPoints)
                {
                    VRect<double> jawPositions = cpParams.JawPositions;
                    VRect<double> newJawPos = new VRect<double>(jawPositions.X1, numLeafClosedY1 * 10 - maxJawY, jawPositions.X2, maxJawY - numLeafClosedY2 * 10);
                    cpParams.JawPositions = newJawPos;
                }
                beam.ApplyParameters(beamParameters);
            }
        }
    }
}
