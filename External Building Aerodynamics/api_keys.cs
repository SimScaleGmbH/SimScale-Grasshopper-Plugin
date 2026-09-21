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

            // This step trips up first-time users more than anything else in setup (see
            // README "Installation" steps 4-5): the file is easy to save with the leading
            // "." stripped by Windows/Explorer, or left in Downloads instead of the home
            // directory. Each failure mode gets its own message pointing at the exact fix,
            // instead of a generic "not found" that leaves the user guessing.
            if (!File.Exists(yamlFilePath))
            {
                throw new FileNotFoundException(
                    $"SimScale API keys file not found at '{yamlFilePath}'. Download the template from " +
                    "examples/.simscale_api_keys.yaml in the plugin repo, save it to your home directory " +
                    $"('{homePath}') as exactly '.simscale_api_keys.yaml' (the leading dot is required and " +
                    "is sometimes stripped by Windows when saving), and fill in your SimScale API key from " +
                    "https://www.simscale.com/dashboard/api_keys.");
            }

            SimScaleAPIKeys loadedKeys;
            try
            {
                string yamlContent = File.ReadAllText(yamlFilePath);
                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build();
                loadedKeys = deserializer.Deserialize<SimScaleAPIKeys>(yamlContent);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not parse '{yamlFilePath}' as YAML — check it matches the format in " +
                    $"examples/.simscale_api_keys.yaml. ({ex.GetType().Name}: {ex.Message})", ex);
            }

            if (loadedKeys?.prod_api_keys == null ||
                string.IsNullOrWhiteSpace(loadedKeys.prod_api_keys.SIMSCALE_API_KEY) ||
                string.IsNullOrWhiteSpace(loadedKeys.prod_api_keys.SIMSCALE_API_URL))
            {
                throw new InvalidOperationException(
                    $"'{yamlFilePath}' is missing 'prod_api_keys.SIMSCALE_API_KEY' or 'SIMSCALE_API_URL' — " +
                    "compare it against examples/.simscale_api_keys.yaml and make sure your API key was pasted in.");
            }

            this.prod_api_keys = loadedKeys.prod_api_keys;
        }
    }
}
