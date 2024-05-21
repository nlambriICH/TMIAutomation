using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Serilog;

namespace TMIAutomation
{
    public static class Client
    {
        private static readonly ILogger logger = Log.ForContext(typeof(Client));
        private static readonly string serverConfig = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "dist", "app", "config.yml");
        public static readonly string MODEL_NAME_BODY = "body_cnn";
        public static readonly string MODEL_NAME_ARMS = "arms_cnn";
        public static readonly bool collPelvis = GetCollPelvis();

        public static Dictionary<string, List<List<double>>> GetFieldGeometry(string modelName, string dicomPath, string upperPTVId, List<string> oarIds)
        {
            Dictionary<string, List<List<double>>> fieldGeometry = new Dictionary<string, List<List<double>>> { };

            ClientRequest request = new ClientRequest
            {
                ModelName = modelName,
                DicomPath = dicomPath,
                IdPTV = upperPTVId,
                IdOARs = oarIds
            };

            int retries = 5;
            logger.Information("Sending request {@request}", request);

            using (HttpClient client = new HttpClient())
            {
                while (retries >= 0)
                {
                    // Redefine content because client disposes it after request
                    StringContent content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

                    // Read port because seerver can start and modify it after client read
                    int? port = GetServerPort();
                    if (port == null) throw new InvalidOperationException("Could not retrieve the server port.");

                    try
                    {
                        HttpResponseMessage response = client.PostAsync($"http://localhost:{port}/predict", content).Result;
                        string jsonString = response.Content.ReadAsStringAsync().Result;
                        fieldGeometry = JsonConvert.DeserializeObject<Dictionary<string, List<List<double>>>>(jsonString);
                        break;
                    }
                    catch (AggregateException exc)
                    {
                        if (retries == 0)
                        {
                            logger.Error(exc, "Call to web server failed");
                            throw;
                        }
                        logger.Warning("Call to web server failed. Number of remaining retries: {retries}", retries);
                        retries -= 1;
                        Thread.Sleep(TimeSpan.FromMilliseconds(300));
                    }
                }
            }

            return fieldGeometry;
        }

        private static bool GetCollPelvis()
        {
            bool collPelvis = false;
            logger.Verbose("Reading coll_pelvis from {serverConfig}", serverConfig);
            foreach (string line in File.ReadLines(serverConfig))
            {
                if (line.StartsWith("coll_pelvis:"))
                {
                    collPelvis = bool.Parse(line.Split(':').Last());
                    logger.Verbose("Retrieved coll_pelvis {coll_pelvis}", collPelvis);
                    break;
                }
            }

            return collPelvis;
        }

        private static int? GetServerPort()
        {
            int? port = null;
            logger.Verbose("Reading port from {serverConfig}", serverConfig);
            foreach (string line in File.ReadLines(serverConfig))
            {
                if (line.StartsWith("port:"))
                {
                    port = int.Parse(line.Split(':').Last());
                    logger.Verbose("Retrieved port number {port}", port);
                    break;
                }
            }

            return port;
        }

        private class ClientRequest
        {
            [JsonProperty("model_name")]
            public string ModelName { get; set; }
            [JsonProperty("dicom_path")]
            public string DicomPath { get; set; }
            [JsonProperty("ptv_name")]
            public string IdPTV { get; set; }
            [JsonProperty("oars_name")]
            public List<string> IdOARs { get; set; }
        }
    }
}