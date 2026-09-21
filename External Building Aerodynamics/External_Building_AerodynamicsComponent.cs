using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Rhino.FileIO;
using System.IO.Compression;


using External_Building_Aerodynamics;
using static External_Building_Aerodynamics.MeshInterpolator;
using System.IO;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Kitware.VTK;

namespace External_Building_Aerodynamics
{
    public class Upload_Model : GH_Component
    {
        public Upload_Model() : base("Upload Model", "Upload", "Uploads a 3D model to be used in simulation.", "SimScale", "Pre-processing") { }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Site", "Site", "The part of the model that represents the site you are designing or analysing", GH_ParamAccess.list);
            pManager.AddGeometryParameter("Context", "Context", "The part of the model that represents the surrounding buildings", GH_ParamAccess.list);
            pManager.AddGeometryParameter("Topology", "Topology", "The part of the model that represents the surrounding buildings", GH_ParamAccess.list);
            pManager.AddGenericParameter("Additional Geometries", "AddGeom", "Additional named geometries", GH_ParamAccess.list);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Geometry", "Geometry", "An ID that identifies the uploaded geometry", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string[] predefinedNames = { "NS_SITE", "NS_CONTEXT", "NS_TOPOLOGY" };
            List<string> createdStlFiles = new List<string>();

            string homePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "SimScale Geometry");

            try
            {
                if (!System.IO.Directory.Exists(homePath))
                {
                    System.IO.Directory.CreateDirectory(homePath);
                }

                for (int i = 0; i < 3; i++)
                {
                    List<GeometryBase> geometryList = new List<GeometryBase>();
                    if (DA.GetDataList(i, geometryList))
                    {
                        string stlName = ExportGeometryListToStl(geometryList, predefinedNames[i], homePath);
                        if (!string.IsNullOrEmpty(stlName))
                        {
                            createdStlFiles.Add(stlName);
                        }
                    }
                }

                List<AdditionalGeometryInput.NamedGeometry> additionalGeometries = new List<AdditionalGeometryInput.NamedGeometry>();
                if (DA.GetDataList("Additional Geometries", additionalGeometries))
                {
                    foreach (var namedGeometry in additionalGeometries)
                    {
                        string stlName = ExportGeometryListToStl(namedGeometry.Geometries, "NS_" + namedGeometry.Name, homePath);
                        if (!string.IsNullOrEmpty(stlName))
                        {
                            createdStlFiles.Add(stlName);
                        }
                    }
                }

                // Create ZIP
                if (createdStlFiles.Count > 0)
                {
                    string zipName;
                    if (createdStlFiles.Count == 1)
                    {
                        zipName = System.IO.Path.GetFileNameWithoutExtension(createdStlFiles[0]) + ".zip";
                    }
                    else
                    {
                        zipName = "Meshes.zip";
                    }
                    string zipFilePath = System.IO.Path.Combine(homePath, zipName);

                    if (System.IO.File.Exists(zipFilePath))
                    {
                        System.IO.File.Delete(zipFilePath);
                    }

                    using (var zipArchive = System.IO.Compression.ZipFile.Open(zipFilePath, System.IO.Compression.ZipArchiveMode.Create))
                    {
                        foreach (var stlFile in createdStlFiles)
                        {
                            string fullPath = System.IO.Path.Combine(homePath, stlFile);
                            if (System.IO.File.Exists(fullPath))
                            {
                                zipArchive.CreateEntryFromFile(fullPath, stlFile);
                                System.IO.File.Delete(fullPath); // Deleting the STL after adding to zip
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to upload model: {ex.Message}");
            }
        }

        private string ExportGeometryListToStl(List<GeometryBase> geometryList, string name, string path)
        {
            Rhino.Geometry.Mesh joinedMesh = new Rhino.Geometry.Mesh();
            foreach (var geometry in geometryList)
            {
                if (geometry is Rhino.Geometry.Brep)
                {
                    var brep = geometry as Rhino.Geometry.Brep;
                    var meshes = Rhino.Geometry.Mesh.CreateFromBrep(brep, Rhino.Geometry.MeshingParameters.Default);
                    foreach (var submesh in meshes)
                    {
                        joinedMesh.Append(submesh);
                    }
                }
                else if (geometry is Rhino.Geometry.Mesh)
                {
                    joinedMesh.Append(geometry as Rhino.Geometry.Mesh);
                }
            }

            if (joinedMesh == null || joinedMesh.Faces.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to create a mesh from the {name} geometry.");
                return null;
            }

            string filePath = System.IO.Path.Combine(path, $"{name}.stl");

            Guid meshId = Rhino.RhinoDoc.ActiveDoc.Objects.AddMesh(joinedMesh);
            Rhino.RhinoDoc.ActiveDoc.Objects.Select(meshId);

            string scriptCommand = $"-Export \"{filePath}\" _Enter";
            Rhino.RhinoApp.RunScript(scriptCommand, false);

            Rhino.RhinoDoc.ActiveDoc.Objects.UnselectAll();
            Rhino.RhinoDoc.ActiveDoc.Objects.Delete(meshId, true);

            return $"{name}.stl";
        }

        public override Guid ComponentGuid => GuidUtility.CreateDeterministicGuid("component2");
    }
    public class AdditionalGeometryInput : GH_Component
    {
        public AdditionalGeometryInput() : base("Additional Geometry Input", "AddGeom", "Input for additional named geometries.", "SimScale", "Pre-processing") { }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Geometry Name", "Name", "Name for the geometry", GH_ParamAccess.item);
            pManager.AddGeometryParameter("Geometry", "Geometry", "The geometry to be processed", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Named Geometry", "NG", "Named geometry output", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string geometryName = "";
            if (!DA.GetData(0, ref geometryName)) return;

            List<GeometryBase> geometries = new List<GeometryBase>();
            if (!DA.GetDataList(1, geometries)) return;

            DA.SetData(0, new NamedGeometry(geometryName, geometries));
        }

        public class NamedGeometry
        {
            public string Name { get; set; }
            public List<GeometryBase> Geometries { get; set; }

            public NamedGeometry(string name, List<GeometryBase> geometries)
            {
                Name = name;
                Geometries = geometries;
            }
        }

        public override Guid ComponentGuid => GuidUtility.CreateDeterministicGuid("component3");
    }

    public class DownloadResults : GH_Component
    {
        public DownloadResults() : base("Download and process results", "Download", "Downloads multidirectional PWC results.", "SimScale", "Post-Processing") { }

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Project Name", "Project", "The exact Project name including whitespace", GH_ParamAccess.item);
            pManager.AddTextParameter("Simulation Name", "Simulation", "The exact Simulation name including whitespace", GH_ParamAccess.item);
            pManager.AddTextParameter("Simulation Run Name", "Simulation Run", "The exact Simulation Run name including whitespace", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Run?", "Run?", "A boolean to run the component, results might take some time to download and process, so the component will only run when explicitly asked", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_SimScalePWCIntegrationParam(), "SimScale Object", "Obj", "A SimScale object that contains all the information required for later processing", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string projectName = "";
            string simulationName = "";
            string runName = "";
            Boolean download = false;

            if (!DA.GetData(0, ref projectName)) return;
            if (!DA.GetData(1, ref simulationName)) return;
            if (!DA.GetData(2, ref runName)) return;
            if (!DA.GetData(3, ref download)) return;

            if (download)
            {
                try
                {
                    // Instantiate the SimScalePWC class
                    var simScale = new SimScalePWCIntegration();

                    // Find the IDs for the project, simulation, and run
                    var projectId = simScale.FindProjectIdByName(projectName);
                    var simulationId = simScale.FindSimulationIdByName(projectId, simulationName);
                    var runId = simScale.FindRunIdByName(projectId, simulationId, runName);

                    // Download the comfort plot for the specified PWC simulation
                    var (directionPaths, comfortPath) = simScale.DownloadResults(projectId, simulationId, runId);

                    Console.WriteLine("Comfort Plot Path:");
                    Console.WriteLine(comfortPath);

                    Console.WriteLine("\nDirection Paths:");
                    foreach (var path in directionPaths)
                    {
                        Console.WriteLine(path);
                    }

                    var outputPath = Path.Combine(simScale.WorkingDirectory, "SimScale", "speedup");

                    simScale.MeshInterpolator = new MeshInterpolator();
                    simScale.SpeedUpFactorPath = simScale.MeshInterpolator.createSpeedUpFactors(directionPaths, comfortPath, outputPath);

                    // Wrap the SimScalePWCIntegration object in a GH_Goo wrapper
                    var goo = new GH_SimScalePWCIntegrationGoo(simScale);

                    // Set the GH_Goo object as the output
                    DA.SetData(0, goo);
                }
                catch (Exception ex)
                {
                    // Without this, an API/auth failure or a not-found project/simulation/run
                    // name surfaces to the user as Grasshopper's generic "component failed"
                    // dialog with no indication of what went wrong or which of the three
                    // names (project/simulation/run) was the problem.
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to download results: {ex.Message}");
                }
            }
        }

        public override Guid ComponentGuid => GuidUtility.CreateDeterministicGuid("component4");
    }

    public class CollectWindSpeeds : GH_Component
    {
        public CollectWindSpeeds()
          : base("Collect Wind Speeds", "Speeds",
              "Processes a VTU file based on wind direction and speed multiplier.",
              "SimScale", "Post-Processing")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new GH_SimScalePWCIntegrationParam(), "SimScale Object", "Obj", "A SimScale object that contains all the information required for later processing", GH_ParamAccess.item);
            pManager.AddNumberParameter("Wind Direction", "Direction", "Wind direction in degrees", GH_ParamAccess.item);
            pManager.AddNumberParameter("Speed Multiplier", "Speed", "Multiplier for the speed values", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_SimScalePWCIntegrationParam(), "SimScale Object", "Obj", "A SimScale object that contains all the information required for later processing", GH_ParamAccess.item);
            pManager.AddTextParameter("Path", "OutPath", "Path to the processed VTU file", GH_ParamAccess.item);

        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_SimScalePWCIntegrationGoo goo = null;
            double windDirection = 0;
            double speedMultiplier = 1;

            if (!DA.GetData(0, ref goo))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No SimScale object provided.");
                return;
            }

            if (!DA.GetData(1, ref windDirection)) return;
            if (!DA.GetData(2, ref speedMultiplier)) return;

            SimScalePWCIntegration obj = goo.Value;

            try
            {
                singleSpeed speed = new singleSpeed(obj.SpeedUpFactorPath, windDirection, speedMultiplier);
                obj.SingleSpeedPath = speed.ProcessVTU();

                // Wrap the SimScalePWCIntegration object in a GH_Goo wrapper
                goo = new GH_SimScalePWCIntegrationGoo(obj);

                // Set the GH_Goo object as the output
                DA.SetData(0, goo);
                DA.SetData(1, obj.SingleSpeedPath);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        public override Guid ComponentGuid => GuidUtility.CreateDeterministicGuid("component5");
    }

    public class VTUToRhinoMeshComponent : GH_Component
    {
        public VTUToRhinoMeshComponent()
            : base("VTUToRhinoMesh", "Convert",
              "Convert a VTU file to Rhino mesh and values.",
              "SimScale", "Post-Processing")
        {
        }

        public override Guid ComponentGuid => GuidUtility.CreateDeterministicGuid("component6");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("VTU File Path", "Path", "Path to the VTU file", GH_ParamAccess.item);
            pManager.AddTextParameter("Field", "Field", "The name of the field you wish to import", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            // Add an output parameter for the mesh
            pManager.AddMeshParameter("Mesh", "Mesh", "Converted Rhino Mesh", GH_ParamAccess.item);

            // Add an output parameter for the data values
            pManager.AddNumberParameter("Data Values", "Values", "Data values corresponding to mesh vertices", GH_ParamAccess.list);
        }


        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string filePath = null;
            string dataFieldName = null;

            if (!DA.GetData(0, ref filePath)) return;
            if (!DA.GetData(1, ref dataFieldName)) return;

            try
            {
                // Read the VTU file and convert it to a Rhino Mesh with data values
                (GH_Mesh ghMesh, List<GH_Number> ghDataValues) = ConvertVTUToMeshes(filePath, dataFieldName);

                // Set the data
                DA.SetData(0, ghMesh);
                DA.SetDataList(1, ghDataValues); // Ensure you have a second output parameter for these values
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to convert '{filePath}' (field '{dataFieldName}'): {ex.Message}");
            }
        }

        private (GH_Mesh, List<GH_Number>) ConvertVTUToMeshes(string filePath, string dataFieldName)
        {
            // Implement the logic to read VTU file, convert to Rhino Meshes
            // and extract values
            (Mesh rhinoMesh, List<double> dataValues) = VTUToRhinoMesh.ConvertVTUToRhinoMesh(filePath, dataFieldName);

            var ghMesh = new Grasshopper.Kernel.Types.GH_Mesh(rhinoMesh);
            var ghDataValues = new List<Grasshopper.Kernel.Types.GH_Number>();
            foreach (var val in dataValues)
            {
                ghDataValues.Add(new Grasshopper.Kernel.Types.GH_Number(val));
            }

            // Return the Grasshopper mesh and data values
            return (ghMesh, ghDataValues);
        }
    }

    public class VTKArrayNamesComponent : GH_Component
    {
        public VTKArrayNamesComponent()
          : base("Get result names", "ResultNames",
              "Gets the array names from a vtk fields",
              "SimScale", "Post-Processing")
        {
        }

        public override Guid ComponentGuid => GuidUtility.CreateDeterministicGuid("component7");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("VTU File Path", "Path", "Path to the VTU file", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Array Names", "Names", "List of array names in the VTU file", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string vtuFilePath = null;

            if (!DA.GetData(0, ref vtuFilePath)) return;

            try
            {
                // Use the utility method to get array names
                var arrayNames = VTUNames.GetArrayNamesFromVTUFile(vtuFilePath);

                // Set the output
                DA.SetDataList(0, arrayNames);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to read array names from '{vtuFilePath}': {ex.Message}");
            }
        }
    }

    public class SimScalePathsComponent : GH_Component
    {
        public SimScalePathsComponent()
          : base("SimScale Paths", "SimScalePaths",
              "Gets paths from a SimScale object",
              "Category", "Subcategory")
        {
        }

        public override Guid ComponentGuid => GuidUtility.CreateDeterministicGuid("component8");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddParameter(new GH_SimScalePWCIntegrationParam(), "SimScale Object", "Obj", "A SimScale object that contains all the information required for later processing", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Speed Up Factor Path", "SUF", "Speed Up Factor file path", GH_ParamAccess.item);
            pManager.AddTextParameter("Single Speed Path", "SS", "Single Speed file path", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_SimScalePWCIntegrationGoo goo = null;

            if (!DA.GetData(0, ref goo))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No SimScale object provided.");
                return;
            }

            SimScalePWCIntegration obj = goo.Value;

            // Extract the paths
            string speedUpFactorPath = obj.SpeedUpFactorPath;
            string singleSpeedPath = obj.SingleSpeedPath;

            // Set the output data
            DA.SetData(0, speedUpFactorPath);
            DA.SetData(1, singleSpeedPath);
        }
    }

    public class ReduceMeshComponent : GH_Component
    {
        public ReduceMeshComponent()
            : base("Reduce Mesh", "ReduceMesh",
                "Reduces the mesh resolution of a SimScale object",
                "SimScale", "Post-Processing")
        {
        }

        public override Guid ComponentGuid => GuidUtility.CreateDeterministicGuid("component9");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            // Assuming SimScaleObject is a class. Replace with the correct type if it's different.
            pManager.AddParameter(new GH_SimScalePWCIntegrationParam(), "SimScale Object", "Obj", "A SimScale object that contains all the information required for later processing", GH_ParamAccess.item);
            pManager.AddNumberParameter("Target resolution", "Resolution", "Mesh target resolution", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddParameter(new GH_SimScalePWCIntegrationParam(), "SimScale Object", "Obj", "A SimScale object that contains all the information required for later processing", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_SimScalePWCIntegrationGoo goo = null;
            double targetResolution = 0;

            if (!DA.GetData(0, ref goo))
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No SimScale object provided.");
                return;
            }

            if (!DA.GetData(1, ref targetResolution)) return;

            SimScalePWCIntegration obj = goo.Value;

            try
            {
                // Call the reduceMesh method
                obj.reduceMesh(obj.SpeedUpFactorPath, targetResolution);

                // Wrap the SimScalePWCIntegration object in a GH_Goo wrapper
                goo = new GH_SimScalePWCIntegrationGoo(obj);

                // Set the GH_Goo object as the output
                DA.SetData(0, goo);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to reduce mesh: {ex.Message}");
            }
        }
    }

    public class GH_SimScalePWCIntegrationParam : GH_Param<GH_SimScalePWCIntegrationGoo>
    {
        public GH_SimScalePWCIntegrationParam()
          : base("SimScale Integration", "SimScale",
                 "Holds a collection of SimScale PWC Integration objects.",
                 "SimScale", "Parameters", GH_ParamAccess.item)
        {
        }

        public override Guid ComponentGuid
        {
            get { return GuidUtility.CreateDeterministicGuid("component100"); }
        }

        public override GH_Exposure Exposure
        {
            get { return GH_Exposure.primary; }
        }

        // Implement any other necessary methods or properties
    }

}

    
