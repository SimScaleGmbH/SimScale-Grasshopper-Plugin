using System;
using System.Collections.Generic;
using System.IO;

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
                string[] yamlLines = File.ReadAllLines(yamlFilePath);
                prod_api_keys = new ProdApiKeys();
                bool inProdApiKeysSection = false;

                foreach (var line in yamlLines)
                {
                    string trimmedLine = line.Trim();

                    // Check if we are in the "prod_api_keys" section
                    if (trimmedLine.StartsWith("prod_api_keys:"))
                    {
                        inProdApiKeysSection = true;
                        continue; // Skip this line since it's just a section header
                    }

                    // If we are inside the prod_api_keys section, parse the key-value pairs
                    if (inProdApiKeysSection)
                    {
                        // Check if we are out of the section (if we encounter another non-indented block)
                        if (!trimmedLine.StartsWith("SIMSCALE_API_URL") && !trimmedLine.StartsWith("SIMSCALE_API_KEY"))
                        {
                            break; // We have reached the end of the prod_api_keys section
                        }

                        // Split by the first colon to separate key and value
                        var keyValuePair = trimmedLine.Split(new[] { ':' }, 2, StringSplitOptions.RemoveEmptyEntries);

                        if (keyValuePair.Length == 2)
                        {
                            string key = keyValuePair[0].Trim();
                            string value = keyValuePair[1].Trim().Trim('"'); // Remove quotes from the value if present

                            // Assign the corresponding values
                            if (key == "SIMSCALE_API_URL")
                            {
                                prod_api_keys.SIMSCALE_API_URL = value;
                            }
                            else if (key == "SIMSCALE_API_KEY")
                            {
                                prod_api_keys.SIMSCALE_API_KEY = value;
                            }
                        }
                    }
                }

                // Check if both keys were found, otherwise throw an error
                if (string.IsNullOrEmpty(prod_api_keys.SIMSCALE_API_URL) || string.IsNullOrEmpty(prod_api_keys.SIMSCALE_API_KEY))
                {
                    throw new Exception("Required API keys are missing in the YAML file.");
                }
            }
            else
            {
                throw new FileNotFoundException($"SimScale API keys YAML file not found at {yamlFilePath}.");
            }
        }
    }
}

