using SimScale.Sdk.Api;
using SimScale.Sdk.Client;
using SimScale.Sdk.Model;
using System;
using System.IO;
using YamlDotNet.RepresentationModel;
using RestSharp;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Drawing;
using System.Xml.Linq;
using System.IO.Compression;
using SharpZipLib = ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;
using External_Building_Aerodynamics;
using System.Threading;
using Grasshopper.Kernel.Types;
using YamlDotNet.Core.Tokens;
using Grasshopper.Kernel;

namespace External_Building_Aerodynamics
{
    public class SimScalePWCIntegration
    {
        private Configuration config;
        private RestClient restClient;
        private string _apiKey;
        private string _apiUrl;
        private string _apiHeader;

        public string WorkingDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public MeshInterpolator MeshInterpolator { get; set; }
        public string SpeedUpFactorPath { get; set; }
        public string SingleSpeedPath { get; set; }
        public SimScalePWCIntegration()
        {
            ReadAPIConfigFromYAML();
            CreateConfig();
            restClient = new RestClient();
        }
        public SimScalePWCIntegration Clone()
        {
            // Implement cloning logic here
            // This could be as simple as a memberwise clone or a deep copy depending on the nature of the object
            return new SimScalePWCIntegration(/* parameters for cloning */);
        }

        public override string ToString()
        {
            // Return a string representation of the object
            return "SimScalePWCIntegration Object";
        }

        private void ReadAPIConfigFromYAML()
        {
            SimScaleAPIKeys keys = new SimScaleAPIKeys();
            keys.LoadKeysFromYamlFile();
            _apiKey = keys.prod_api_keys.SIMSCALE_API_KEY;
            _apiUrl = keys.prod_api_keys.SIMSCALE_API_URL;

        }

        private void CreateConfig()
        {
            Configuration config = new Configuration();
            config.BasePath = _apiUrl + "/v0";

            _apiHeader = "X-API-KEY";
            config.ApiKey.Add(_apiHeader, _apiKey);

            this.config = config;
        }

        public string FindProjectIdByName(string projectName)
        {
            var projectsApi = new ProjectsApi(this.config);


            int length = 100;
            int i = 1;
            while (length != 0)
            {
                var projects = projectsApi.GetProjects(100, i);
                foreach (var project in projects.Embedded)
                {
                    if (project.Name == projectName)
                    {
                        return project.ProjectId;

                    }
                }
                i += 1;
            }

            throw new Exception($"No project found with the name: {projectName}");
        }

        public Guid? FindSimulationIdByName(string projectId, string simulationName)
        {
            var simulationsApi = new SimulationsApi(config);
            var simulations = simulationsApi.GetSimulations(projectId);

            foreach (var simulation in simulations.Embedded)
            {
                if (simulation.Name == simulationName)
                {
                    return simulation.SimulationId;
                }
            }

            throw new Exception($"No simulation found with the name: {simulationName}");
        }

        public Guid? FindRunIdByName(string projectId, Guid? simulationId, string runName)
        {
            var runsApi = new SimulationRunsApi(config);
            var runs = runsApi.GetSimulationRuns(projectId, simulationId);

            foreach (var run in runs.Embedded)
            {
                if (run.Name == runName)
                {
                    return run.RunId;
                }
            }

            throw new Exception($"No run found with the name: {runName}");
        }

        public (List<string> directionPaths, string comfortPlotPath) DownloadResults(string projectId, Guid? simulationId, Guid? runId)
        {
            var simulationRunApi = new SimulationRunsApi(config);

            // Download averaged direction results
            var directionResults = simulationRunApi.GetSimulationRunResults(
                projectId: projectId,
                simulationId: simulationId,
                runId: runId,
                type: "SOLUTION_FIELD",
                category: "AVERAGED_SOLUTION"
            );

            // Download comfort plot
            var comfortPlotResults = simulationRunApi.GetSimulationRunResults(
                projectId: projectId,
                simulationId: simulationId,
                runId: runId,
                type: "SOLUTION_FIELD",
                category: "STATISTICAL_SURFACE_SOLUTION"
            );

            List<string> directionPaths = new List<string>();
            string homePath = WorkingDirectory;

            string caseFilePath = "";  // For storing the .case file path

            foreach (var result in directionResults.Embedded)
            {
                if (result is SimulationRunResultSolution solutionResult)
                {
                    var statisticalSurfaceSolutionInfo = (SimulationRunResultSolution)result;

                    float direction = (float)statisticalSurfaceSolutionInfo.Direction;

                    var statisticalSurfaceSolutionRequest = new RestRequest(statisticalSurfaceSolutionInfo.Download.Url, Method.GET);
                    statisticalSurfaceSolutionRequest.AddHeader(_apiHeader, _apiKey);

                    var fileName = $"{direction}.zip";
                    var directionsPath = Path.Combine(homePath, "SimScale", "Directions");

                    Directory.CreateDirectory(directionsPath);

                    var zipPath = Path.Combine(directionsPath, fileName);

                    using (var writer = File.OpenWrite(zipPath))
                    {
                        statisticalSurfaceSolutionRequest.ResponseWriter = responseStream =>
                        {
                            using (responseStream)
                            {
                                responseStream.CopyTo(writer);
                            }
                        };
                        restClient.DownloadData(statisticalSurfaceSolutionRequest);
                        writer.Flush();  // Ensures all data is written to the file
                    }
                    Thread.Sleep(5000);

                    string directoryPath = Path.Combine(directionsPath, direction.ToString());

                    using (FileStream fs = File.OpenRead(zipPath))
                    {
                        using (SharpZipLib.ZipFile zf = new SharpZipLib.ZipFile(fs))
                        {
                            foreach (SharpZipLib.ZipEntry zipEntry in zf)
                            {
                                if (!zipEntry.IsFile)
                                {
                                    continue; // Ignore directories
                                }

                                string entryFileName = zipEntry.Name.Replace('/', Path.DirectorySeparatorChar); // Correcting the path
                                string fullZipToPath = Path.Combine(directoryPath, entryFileName);

                                // Ensure the directory exists
                                string directoryName = Path.GetDirectoryName(fullZipToPath);
                                if (directoryName.Length > 0)
                                    Directory.CreateDirectory(directoryName);

                                try
                                {
                                    using (Stream zipStream = zf.GetInputStream(zipEntry))
                                    using (FileStream streamWriter = File.Create(fullZipToPath))
                                    {
                                        zipStream.CopyTo(streamWriter);
                                    }

                                    if (entryFileName.EndsWith(".case"))
                                    {
                                        caseFilePath = fullZipToPath;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to extract file at path: {fullZipToPath}");
                                    throw new Exception($"Extraction failed for path: {fullZipToPath}", ex);
                                }
                            }
                        }
                    }

                    directionPaths.Add(caseFilePath);
                }
            }

            string comfortPlotPath = "";
            string vtmFilePath = "";  // For storing the .vtm file path

            if (comfortPlotResults.Embedded.Count > 0 && comfortPlotResults.Embedded[0] is SimulationRunResultSolution comfortSolutionResult)
            {
                var comfortPlotInfo = (SimulationRunResultSolution)comfortPlotResults.Embedded[0];

                var comfortPlotRequest = new RestRequest(comfortPlotInfo.Download.Url, Method.GET);
                comfortPlotRequest.AddHeader(_apiHeader, _apiKey);

                var comfortPlotsPath = Path.Combine(homePath, "SimScale", "ComfortPlots");
                Directory.CreateDirectory(comfortPlotsPath);

                var zipPath = Path.Combine(comfortPlotsPath, "comfort_plot.zip");

                using (var writer = File.OpenWrite(zipPath))
                {
                    comfortPlotRequest.ResponseWriter = responseStream => responseStream.CopyTo(writer);
                    restClient.DownloadData(comfortPlotRequest);
                }

                comfortPlotPath = Path.Combine(comfortPlotsPath, "ComfortPlot");

                using (FileStream fs = File.OpenRead(zipPath))
                {
                    using (SharpZipLib.ZipFile zf = new SharpZipLib.ZipFile(fs))
                    {
                        foreach (SharpZipLib.ZipEntry zipEntry in zf)
                        {
                            string correctedEntryPath = zipEntry.Name.Replace('/', Path.DirectorySeparatorChar);
                            string destinationPath = Path.Combine(comfortPlotPath, correctedEntryPath);

                            if (zipEntry.IsDirectory)
                            {
                                Directory.CreateDirectory(destinationPath);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                                try
                                {
                                    using (var zipStream = zf.GetInputStream(zipEntry))
                                    {
                                        using (FileStream output = File.Create(destinationPath))
                                        {
                                            StreamUtils.Copy(zipStream, output, new byte[4096]);
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Failed to extract file at path: {destinationPath}");
                                    throw new Exception($"Extraction failed for path: {destinationPath}", ex);
                                }

                                if (correctedEntryPath.EndsWith(".vtm"))
                                {
                                    vtmFilePath = destinationPath;
                                }
                            }
                        }
                    }
                }


                comfortPlotPath = vtmFilePath;
            }

            return (directionPaths, comfortPlotPath);
        }

        public void reduceMesh(string path, double reductionFactor)
        {
            string outputPath = Path.Combine(WorkingDirectory, "SimScale", "speedup", "speed_up_factors_reduced.vtu");
            MeshInterpolator.MeshReduction.ReduceMeshResolution(path, outputPath, reductionFactor);

            SpeedUpFactorPath = outputPath;
        }

        // Additional methods or logic if needed
    }

    public class GH_SimScalePWCIntegrationGoo : GH_Goo<SimScalePWCIntegration>
    {
        public override bool IsValid => Value != null;

        public override string TypeName => "SimScalePWCIntegration";

        public override string TypeDescription => "A wrapper for SimScalePWCIntegration object";

        public GH_SimScalePWCIntegrationGoo()
        {
            this.Value = null;
        }

        public GH_SimScalePWCIntegrationGoo(SimScalePWCIntegration simScalePWC)
        {
            this.Value = simScalePWC;
        }

        public override IGH_Goo Duplicate()
        {
            if (Value == null) return new GH_SimScalePWCIntegrationGoo();
            return new GH_SimScalePWCIntegrationGoo(Value.Clone()); // Implement Clone method in your class if necessary
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null SimScalePWCIntegration";
            return Value.ToString(); // Implement ToString method in your class for better description
        }

        // Implement other necessary overrides...
    }

    
}