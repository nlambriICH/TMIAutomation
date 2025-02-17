﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TMIAutomation.Tests.Attributes;
using Xunit;
using Xunit.Sdk;


namespace TMIAutomation.Tests
{
    class ClientTests : TestBase
    {
        private string patientID;
        private static readonly string executingPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        public override ITestBase Init(object testObject, params object[] optParams)
        {
            this.patientID = optParams.OfType<string>().FirstOrDefault() as string;
            return this;
        }

        private static Process StartLocalServer()
        {
            string serverDirectory = Path.Combine(executingPath, "dist", "app");
            string serverPath = Path.Combine(serverDirectory, "app.exe");
            ProcessStartInfo startInfo = new ProcessStartInfo(serverPath)
            {
                WorkingDirectory = serverDirectory,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            return Process.Start(startInfo);
        }

        [Theory]
        [MemberData(nameof(GetFieldGeometry_Data))]
        private void GetFieldGeometry(string modelName,
                                      List<List<double>> expectedIsocenters,
                                      List<List<double>> expectedJawX,
                                      List<List<double>> expectedJawY)
        {
            Process serverProcess = StartLocalServer();
            try
            {
                Dictionary<string, List<List<double>>> fieldGeometry = Client.GetFieldGeometry(modelName,
                                                                                               Path.Combine(executingPath, "Dicoms", this.patientID),
                                                                                               "PTV_totFIN_Crop",
                                                                                               new List<string> { "Encefalo", "Polmone SX", "Polmone DX", "Fegato", "Intestino", "Vescica" });

                List<List<double>> isocenters = Assert.Contains("Isocenters", fieldGeometry as IReadOnlyDictionary<string, List<List<double>>>);
                List<List<double>> jawX = Assert.Contains("Jaw_X", fieldGeometry as IReadOnlyDictionary<string, List<List<double>>>);
                List<List<double>> jawY = Assert.Contains("Jaw_Y", fieldGeometry as IReadOnlyDictionary<string, List<List<double>>>);

                for (int i = 0; i < isocenters.Count; ++i)
                {
                    for (int j = 0; j < isocenters[i].Count; ++j)
                    {
                        Assert.Equal(expectedIsocenters[i][j], isocenters[i][j], 2);
                    }

                    for (int j = 0; j < jawX[i].Count; ++j)
                    {
                        Assert.Equal(expectedJawX[i][j], jawX[i][j], 2);
                        Assert.Equal(expectedJawY[i][j], jawY[i][j], 2);
                    }

                }
            }
            catch (ContainsException e)
            {
                throw new Exception($"Could not find expected dictionary key", e);
            }
            catch (EqualException)
            {
                // Re-throw exception to shut down server
                throw;
            }
            finally
            {
                serverProcess?.Kill();
            }
        }

        public static IEnumerable<object[]> GetFieldGeometry_Data()
        {
            if (Client.collPelvis)
            {
                yield return new object[] {
                    Client.MODEL_NAME_BODY,
                    new List<List<double>> { // isocenters
                        new List<double> { 25.98, 118.0, -846.51 },
                        new List<double> { 25.98, 118.0, -846.51 },
                        new List<double> { 25.98, 118.0, -536.60 },
                        new List<double> { 25.98, 118.0, -536.60 },
                        new List<double> { 25.98, 118.0, -371.47 },
                        new List<double> { 25.98, 118.0, -371.47 },
                        new List<double> { 25.98, 118.0, -206.35 },
                        new List<double> { 25.98, 118.0, -206.35 },
                        new List<double> { 25.98, 118.0, -6.51 },
                        new List<double> { 25.98, 118.0, -6.51 },
                        new List<double> { -300.0, 118.0, 163.40 },
                        new List<double> { -300.0, 118.0, 163.40 },
                    },
                    new List<List<double>> { // jawX
                        new List<double> { -170.0, 30.0 },
                        new List<double> { -30.0, 170.0 },
                        new List<double> { -5.0, 107.56 },
                        new List<double> { -159.91, 5.0 },
                        new List<double> { -9.99, 107.56 },
                        new List<double> { -107.56, 9.99 },
                        new List<double> { -10.41, 108.06 },
                        new List<double> { -107.56, 10.41 },
                        new List<double> { -9.51, 124.91 },
                        new List<double> { -117.68, 9.51 },
                        new List<double> { -200.0, -200.0 },
                        new List<double> { -200.0, -200.0 },
                    },
                    new List<List<double>> { // jawY
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -123.18, 131.96 },
                        new List<double> { -114.01, 118.91 },
                        new List<double> { -163.40, -163.40 },
                        new List<double> { -163.40, -163.40 },
                    }
                };
                yield return new object[] {
                    Client.MODEL_NAME_ARMS,
                    new List<List<double>> { // isocenters
                        new List<double> { 25.98, 118.0, -837.16 },
                        new List<double> { 25.98, 118.0, -837.16 },
                        new List<double> { 25.98, 118.0, -534.10 },
                        new List<double> { 25.98, 118.0, -534.10 },
                        new List<double> { 25.98, 118.0, 163.40 },
                        new List<double> { 25.98, 118.0, 163.40 },
                        new List<double> { 25.98, 118.0, -312.69 },
                        new List<double> { 25.98, 118.0, -312.69 },
                        new List<double> { 25.98, 118.0, -91.28 },
                        new List<double> { 25.98, 118.0, -91.28 },
                        new List<double> { -180.85, 118.0, -502.77 },
                        new List<double> { 195.97, 118.0, -502.77 },
                    },
                    new List<List<double>> { // jawX
                        new List<double> { -170.0, 30.0 },
                        new List<double> { -30.0, 170.0 },
                        new List<double> { -3.75, 124.11 },
                        new List<double> { -153.06, 1.25 },
                        new List<double> { 25.98, 25.98 },
                        new List<double> { 25.98, 25.98 },
                        new List<double> { -9.71, 145.70 },
                        new List<double> { -147.30, 9.71 },
                        new List<double> { -9.36, 200.0 },
                        new List<double> { -125.71, 9.36 },
                        new List<double> { -93.53, 66.37 },
                        new List<double> { -75.47, 74.32 },
                    },
                    new List<List<double>> { // jawY
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -163.40, -163.40 },
                        new List<double> { -163.40, -163.40 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -123.26, 123.05 },
                        new List<double> { -145.20, 145.89 },
                        new List<double> { -200.0, 200.0 },
                        new List<double> { -200.0, 200.0 },
                    }
                };
            }
            else
            {
                yield return new object[] {
                    Client.MODEL_NAME_BODY,
                    new List<List<double>> { // isocenters
                        new List<double> { 25.98, 118.0, -844.55 },
                        new List<double> { 25.98, 118.0, -844.55 },
                        new List<double> { 25.98, 118.0, -640.87 },
                        new List<double> { 25.98, 118.0, -640.87 },
                        new List<double> { 25.98, 118.0, -423.88 },
                        new List<double> { 25.98, 118.0, -423.88 },
                        new List<double> { 25.98, 118.0, -206.89 },
                        new List<double> { 25.98, 118.0, -206.89 },
                        new List<double> { 25.98, 118.0, -4.55 },
                        new List<double> { 25.98, 118.0, -4.55 },
                        new List<double> { -300.0, 118.0, 163.40 },
                        new List<double> { -300.0, 118.0, 163.40 },
                    },
                    new List<List<double>> { // jawX
                        new List<double> { -15.51, 126.84 },
                        new List<double> { -157.05, 14.18 },
                        new List<double> { -9.55, 109.27 },
                        new List<double> { -126.84, 11.54 },
                        new List<double> { -8.99, 138.03 },
                        new List<double> { -117.72, 8.99 },
                        new List<double> { -8.36, 110.68 },
                        new List<double> { -128.96, 8.36 },
                        new List<double> { -9.48, 122.95 },
                        new List<double> { -117.56, 9.48 },
                        new List<double> { -200.0, -200.0 },
                        new List<double> { -200.0, -200.0 },
                    },
                    new List<List<double>> { // jawY
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -121.48, 130.08 },
                        new List<double> { -117.84, 120.81 },
                        new List<double> { -163.40, -163.40 },
                        new List<double> { -163.40, -163.40 },
                    }
                };
                yield return new object[] {
                    Client.MODEL_NAME_ARMS,
                    new List<List<double>> { // isocenters
                        new List<double> { 25.98, 118.0, -811.53 },
                        new List<double> { 25.98, 118.0, -811.53 },
                        new List<double> { 25.98, 118.0, -534.10 },
                        new List<double> { 25.98, 118.0, -534.10 },
                        new List<double> { 25.98, 118.0, 163.40 },
                        new List<double> { 25.98, 118.0, 163.40 },
                        new List<double> { 25.98, 118.0, -260.63 },
                        new List<double> { 25.98, 118.0, -260.63 },
                        new List<double> { 25.98, 118.0, -16.73 },
                        new List<double> { 25.98, 118.0, -16.73 },
                        new List<double> { -185.50, 118.0, -510.19 },
                        new List<double> { 201.95, 118.0, -510.19 },
                    },
                    new List<List<double>> { // jawX
                        new List<double> { -28.67, 185.08 },
                        new List<double> { -190.07, 13.12 },
                        new List<double> { -3.75, 179.77 },
                        new List<double> { -142.34, 1.25 },
                        new List<double> { 25.98, 25.98 },
                        new List<double> { 25.98, 25.98 },
                        new List<double> { -10.19, 161.93 },
                        new List<double> { -143.70, 10.19 },
                        new List<double> { -10.85, 135.13 },
                        new List<double> { -120.82, 10.85 },
                        new List<double> { -80.12, 64.34 },
                        new List<double> { -75.08, 78.40 },
                    },
                    new List<List<double>> { // jawY
                        new List<double> { -200.0, 200.0 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200 },
                        new List<double> { -163.40, -163.40 },
                        new List<double> { -163.40, -163.40 },
                        new List<double> { -200, 200 },
                        new List<double> { -200, 200},
                        new List<double> { -123.95, 119.84 },
                        new List<double> { -133.82, 133.24 },
                        new List<double> { -200.0, 200.0 },
                        new List<double> { -200.0, 200.0 },
                    }
                };
            }
        }
    }
}