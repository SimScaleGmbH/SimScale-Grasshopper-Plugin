using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace External_Building_Aerodynamics
{
    public class SimScaleAPIKeys
    {
        public ProdApiKeys prod_api_keys { get; set; }

        public class ProdApiKeys
        {
            public string SIMSCALE_API_URL { get; set; }
            public string SIMSCALE_API_KEY { get; set; }
        }

        public void LoadKeysFromYamlFile()
        {
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string yamlFilePath = Path.Combine(homePath, ".simscale_api_keys.yaml");

            if (File.Exists(yamlFilePath))
            {
                string yamlContent = File.ReadAllText(yamlFilePath);
                var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
                var loadedKeys = deserializer.Deserialize<SimScaleAPIKeys>(yamlContent);

                this.prod_api_keys = loadedKeys.prod_api_keys;
            }
            else
            {
                throw new FileNotFoundException($"SimScale API keys YAML file not found at {yamlFilePath}.");
            }
        }
    }
}
