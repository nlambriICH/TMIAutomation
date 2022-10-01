using System.IO;
using YamlDotNet.Serialization;

namespace TMIAutomation.Runner.Persistence
{
    internal class DataRepository : IDataRepository
    {
        private readonly string path;
        private readonly ISerializer serializer;
        private readonly IDeserializer deserializer;

        public DataRepository(string path)
        {
            this.path = path;
            this.serializer = new SerializerBuilder().Build();
            this.deserializer = new DeserializerBuilder().Build();
        }

        public Data Load()
        {
            if (!File.Exists(path))
            {
                return new Data();
            }

            string dataString = File.ReadAllText(path);
            return deserializer.Deserialize<Data>(dataString);
        }

        public void Save(Data data)
        {
            string dataString = serializer.Serialize(data);
            File.WriteAllText(path, dataString);
        }
    }
}