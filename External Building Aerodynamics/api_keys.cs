using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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

            // Hand-rolled instead of a YAML library: Rhino 8 itself ships its own
            // YamlDotNet.dll (an older, incompatible version) in its System folder, and
            // loads it before any plugin does. Once that's loaded, this plugin's own
            // YamlDotNet 13.x — even though it's the correct file, sitting right next to
            // this assembly — fails to resolve ("Could not load file or assembly
            // 'YamlDotNet, Version=13.0.0.0'... Could not find or load a specific file"),
            // because .NET's default (non-isolated) load context won't load a second,
            // differently-versioned assembly of the same simple name. The file this reads
            // only ever has two fixed key/value pairs under one section, so a tiny parser
            // avoids the whole collision rather than trying to out-version whatever Rhino
            // happens to bundle.
            string apiUrl = null;
            string apiKey = null;
            try
            {
                bool inProdApiKeys = false;
                foreach (string rawLine in File.ReadAllLines(yamlFilePath))
                {
                    if (string.IsNullOrWhiteSpace(rawLine) || rawLine.TrimStart().StartsWith("#"))
                    {
                        continue;
                    }

                    var match = Regex.Match(rawLine, @"^(?<indent>\s*)(?<key>[A-Za-z0-9_]+)\s*:\s*(?<value>.*)$");
                    if (!match.Success)
                    {
                        continue;
                    }

                    string key = match.Groups["key"].Value;
                    bool isTopLevel = match.Groups["indent"].Value.Length == 0;

                    if (isTopLevel)
                    {
                        inProdApiKeys = key == "prod_api_keys";
                        continue;
                    }

                    if (!inProdApiKeys)
                    {
                        continue;
                    }

                    string value = match.Groups["value"].Value.Trim().Trim('"', '\'');
                    if (key == "SIMSCALE_API_URL")
                    {
                        apiUrl = value;
                    }
                    else if (key == "SIMSCALE_API_KEY")
                    {
                        apiKey = value;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not parse '{yamlFilePath}' as YAML — check it matches the format in " +
                    $"examples/.simscale_api_keys.yaml. ({ex.GetType().Name}: {ex.Message})", ex);
            }

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiUrl))
            {
                throw new InvalidOperationException(
                    $"'{yamlFilePath}' is missing 'prod_api_keys.SIMSCALE_API_KEY' or 'SIMSCALE_API_URL' — " +
                    "compare it against examples/.simscale_api_keys.yaml and make sure your API key was pasted in.");
            }

            this.prod_api_keys = new ProdApiKeys
            {
                SIMSCALE_API_URL = apiUrl,
                SIMSCALE_API_KEY = apiKey
            };
        }
    }
}
