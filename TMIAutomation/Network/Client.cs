using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Serilog;

namespace TMIAutomation
{
    public static class Client
    {
        private static readonly ILogger logger = Log.ForContext(typeof(Client));
        public static readonly string MODEL_NAME_BODY = "body_cnn";
        public static readonly string MODEL_NAME_ARMS = "arms_cnn";

        public static Dictionary<string, List<List<double>>> GetFieldGeometry(string modelName, string dicomPath, string upperPTVId, List<string> oarIds)
        {
            Dictionary<string, List<List<double>>> fieldGeometry = new Dictionary<string, List<List<double>>> { };
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    ClientRequest request = new ClientRequest
                    {
                        ModelName = modelName,
                        DicomPath = dicomPath,
                        IdPTV = upperPTVId,
                        IdOARs = oarIds
                    };

                    int port = GetServerPort() ?? throw new InvalidOperationException("Could not retrieve the server port.");
                    StringContent content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");

                    logger.Information("Sending request {@request}", request);
                    HttpResponseMessage response = client.PostAsync($"http://localhost:{port}/predict", content).Result;
                    if (response != null)
                    {
                        string jsonString = response.Content.ReadAsStringAsync().Result;
                        fieldGeometry = JsonConvert.DeserializeObject<Dictionary<string, List<List<double>>>>(jsonString);
                    }
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Call to web server failed");
            }

            return fieldGeometry;
        }

        private static int? GetServerPort()
        {
            int? port = null;
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string serverConfig = Path.Combine(assemblyDir, "dist", "app", "config.yml");
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
