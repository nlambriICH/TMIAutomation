using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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
                WindowStyle = ProcessWindowStyle.Minimized
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
                Thread.Sleep(TimeSpan.FromSeconds(15)); // bad practice but easier way to wait the server startup
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
                serverProcess?.CloseMainWindow();
            }
        }

        public static IEnumerable<object[]> GetFieldGeometry_Data()
        {
            yield return new object[] {
                "body_cnn",
                new List<List<double>> { // isocenters
                    new List<double> { 25.98, 118.0, -844.55 },
                    new List<double> { 25.98, 118.0, -844.55 },
                    new List<double> { 25.98, 118.0, -640.87 },
                    new List<double> { 25.98, 118.0, -640.87 },
                    new List<double> { 25.98, 118.0, -457.58 },
                    new List<double> { 25.98, 118.0, -457.58 },
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
                    new List<double> { -8.99, 171.74 },
                    new List<double> { -84.02, 8.99 },
                    new List<double> { -8.36, 110.68 },
                    new List<double> { -128.96, 8.36 },
                    new List<double> { -9.48, 129.17 },
                    new List<double> { -117.56, 9.48 },
                    new List<double> { -200.0, -200.0 },
                    new List<double> { -200.0, -200.0 },
                },
                new List<List<double>> { // jawY
                    new List<double> { -193.77, 193.77 },
                    new List<double> { -193.19, 193.19 },
                    new List<double> { -198.61, 198.61 },
                    new List<double> { -198.61, 198.61 },
                    new List<double> { -198.61, 198.61 },
                    new List<double> { -198.61, 198.61 },
                    new List<double> { -198.61, 198.61 },
                    new List<double> { -198.61, 198.61 },
                    new List<double> { -121.48, 130.08 },
                    new List<double> { -117.84, 120.81 },
                    new List<double> { -163.40, -163.40 },
                    new List<double> { -163.40, -163.40 },
                }
            };
            yield return new object[] {
                "arms_cnn",
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
                    new List<double> { -10.85, 119.56 },
                    new List<double> { -120.82, 10.85 },
                    new List<double> { -80.12, 64.34 },
                    new List<double> { -75.08, 78.40 },
                },
                new List<List<double>> { // jawY
                    new List<double> { -200.0, 200.0 },
                    new List<double> { -197.22, 197.22 },
                    new List<double> { -197.22, 197.22 },
                    new List<double> { -197.22, 197.22 },
                    new List<double> { -163.40, -163.40 },
                    new List<double> { -163.40, -163.40 },
                    new List<double> { -197.22, 197.22 },
                    new List<double> { -197.22, 197.22 },
                    new List<double> { -123.95, 119.84 },
                    new List<double> { -133.82, 133.24 },
                    new List<double> { -200.0, 200.0 },
                    new List<double> { -200.0, 200.0 },
                }
            };
        }

    }
}
