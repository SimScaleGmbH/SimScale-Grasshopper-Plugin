﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

using Grasshopper;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Rhino.FileIO;
using System.IO.Compression;


using Guid_Utilities;

namespace External_Building_Aerodynamics
{
    
    public class External_Building_AerodynamicsComponent : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        ///

        

        public External_Building_AerodynamicsComponent()
          : base("ASpi", "ASpi",
            "Construct an Archimedean, or arithmetic, spiral given its radii and number of turns.",
            "Curve", "Primitive")
        {
        }
        

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Use the pManager object to register your input parameters.
            // You can often supply default values when creating parameters.
            // All parameters must have the correct access type. If you want 
            // to import lists or trees of values, modify the ParamAccess flag.
            pManager.AddPlaneParameter("Plane", "P", "Base plane for spiral", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddNumberParameter("Inner Radius", "R0", "Inner radius for spiral", GH_ParamAccess.item, 1.0);
            pManager.AddNumberParameter("Outer Radius", "R1", "Outer radius for spiral", GH_ParamAccess.item, 10.0);
            pManager.AddIntegerParameter("Turns", "T", "Number of turns between radii", GH_ParamAccess.item, 10);

            // If you want to change properties of certain parameters, 
            // you can use the pManager instance to access them by index:
            //pManager[0].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Use the pManager object to register your output parameters.
            // Output parameters do not have default values, but they too must have the correct access type.
            pManager.AddCurveParameter("Spiral", "S", "Spiral curve", GH_ParamAccess.item);

            // Sometimes you want to hide a specific parameter from the Rhino preview.
            // You can use the HideParameter() method as a quick way:
            //pManager.HideParameter(0);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // First, we need to retrieve all data from the input parameters.
            // We'll start by declaring variables and assigning them starting values.
            Plane plane = Plane.WorldXY;
            double radius0 = 0.0;
            double radius1 = 0.0;
            int turns = 0;

            // Then we need to access the input parameters individually. 
            // When data cannot be extracted from a parameter, we should abort this method.
            if (!DA.GetData(0, ref plane)) return;
            if (!DA.GetData(1, ref radius0)) return;
            if (!DA.GetData(2, ref radius1)) return;
            if (!DA.GetData(3, ref turns)) return;

            // We should now validate the data and warn the user if invalid data is supplied.
            if (radius0 < 0.0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Inner radius must be bigger than or equal to zero");
                return;
            }
            if (radius1 <= radius0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Outer radius must be bigger than the inner radius");
                return;
            }
            if (turns <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Spiral turn count must be bigger than or equal to one");
                return;
            }

            // We're set to create the spiral now. To keep the size of the SolveInstance() method small, 
            // The actual functionality will be in a different method:
            Curve spiral = CreateSpiral(plane, radius0, radius1, turns);

            // Finally assign the spiral to the output parameter.
            DA.SetData(0, spiral);
        }

        Curve CreateSpiral(Plane plane, double r0, double r1, Int32 turns)
        {
            Line l0 = new Line(plane.Origin + r0 * plane.XAxis, plane.Origin + r1 * plane.XAxis);
            Line l1 = new Line(plane.Origin - r0 * plane.XAxis, plane.Origin - r1 * plane.XAxis);

            Point3d[] p0;
            Point3d[] p1;

            l0.ToNurbsCurve().DivideByCount(turns, true, out p0);
            l1.ToNurbsCurve().DivideByCount(turns, true, out p1);

            PolyCurve spiral = new PolyCurve();

            for (int i = 0; i < p0.Length - 1; i++)
            {
                Arc arc0 = new Arc(p0[i], plane.YAxis, p1[i + 1]);
                Arc arc1 = new Arc(p1[i + 1], -plane.YAxis, p0[i + 1]);

                spiral.Append(arc0);
                spiral.Append(arc1);
            }

            return spiral;
        }

        /// <summary>
        /// The Exposure property controls where in the panel a component icon 
        /// will appear. There are seven possible locations (primary to septenary), 
        /// each of which can be combined with the GH_Exposure.obscure flag, which 
        /// ensures the component will only be visible on panel dropdowns.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => GuidUtility.CreateDeterministicGuid("component1");
    }
    public class Upload_Model : GH_Component
    {
        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public Upload_Model()
            : base("Upload Model", "Upload",
            "Uploads a 3D model to be used in simulation.",
            "SimScale", "Pre-processing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            // Use the pManager object to register your input parameters.
            // You can often supply default values when creating parameters.
            // All parameters must have the correct access type. If you want 
            // to import lists or trees of values, modify the ParamAccess flag.

            pManager.AddGeometryParameter("Site", "Site",
                "The part of the model that reprasents the site you are designing or analysing",
                GH_ParamAccess.item);
            pManager.AddGeometryParameter("Context", "Context",
                "The part of the model that reprasents the surounding buildings",
                GH_ParamAccess.item);
            pManager.AddGeometryParameter("Topology", "Topology",
                "The part of the model that reprasents the surounding buildings",
                GH_ParamAccess.item);

            // If you want to change properties of certain parameters, 
            // you can use the pManager instance to access them by index:
            //pManager[0].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            // Use the pManager object to register your output parameters.
            // Output parameters do not have default values, but they too must have the correct access type.
            pManager.AddTextParameter("Geometry", "Geometry",
                "An ID that identifies the uploaded geometry",
                GH_ParamAccess.item);

            // Sometimes you want to hide a specific parameter from the Rhino preview.
            // You can use the HideParameter() method as a quick way:
            //pManager.HideParameter(0);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string[] names = { "NS_SITE", "NS_CONTEXT", "NS_TOPOLOGY" };
            string homePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "SimScale Geometry");

            // Check if the homePath exists, if not, create it
            if (!System.IO.Directory.Exists(homePath))
            {
                System.IO.Directory.CreateDirectory(homePath);
            }

            for (int i = 0; i < 3; i++)
            {
                Rhino.Geometry.GeometryBase geometry = null;
                if (!DA.GetData(i, ref geometry)) continue;

                Rhino.Geometry.Mesh meshToExport = null;

                if (geometry is Rhino.Geometry.Brep)
                {
                    var brep = geometry as Rhino.Geometry.Brep;
                    var meshes = Rhino.Geometry.Mesh.CreateFromBrep(brep, Rhino.Geometry.MeshingParameters.Default);
                    Rhino.Geometry.Mesh joinedMesh = new Rhino.Geometry.Mesh();
                    foreach (var submesh in meshes)
                    {
                        joinedMesh.Append(submesh);
                    }
                    meshToExport = joinedMesh;
                }
                else if (geometry is Rhino.Geometry.Mesh)
                {
                    meshToExport = geometry as Rhino.Geometry.Mesh;
                }

                if (meshToExport == null || meshToExport.Faces.Count == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to create a mesh from the {names[i]} geometry.");
                    continue;
                }

                string filePath = System.IO.Path.Combine(homePath, $"{names[i]}.stl");

                // Temporarily add the mesh to the document
                Guid meshId = Rhino.RhinoDoc.ActiveDoc.Objects.AddMesh(meshToExport);

                // Select the mesh in the document
                Rhino.RhinoDoc.ActiveDoc.Objects.Select(meshId);

                // Use the script to export only the selected mesh
                string scriptCommand = $"-Export \"{filePath}\" _Enter";
                Rhino.RhinoApp.RunScript(scriptCommand, false);

                // Deselect the mesh after export
                Rhino.RhinoDoc.ActiveDoc.Objects.UnselectAll();

                // Remove the mesh from the document
                Rhino.RhinoDoc.ActiveDoc.Objects.Delete(meshId, true);
            }

            // ... [previous code]

            //string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string zipFilePath = System.IO.Path.Combine(homePath, "Meshes.zip");

            // Check if the Zip file already exists and delete it if it does
            if (System.IO.File.Exists(zipFilePath))
            {
                System.IO.File.Delete(zipFilePath);
            }

            using (var zipArchive = System.IO.Compression.ZipFile.Open(zipFilePath, System.IO.Compression.ZipArchiveMode.Create))
            {
                string[] stlFiles = { "NS_SITE.stl", "NS_CONTEXT.stl", "NS_TOPOLOGY.stl" };

                foreach (var stlFile in stlFiles)
                {
                    string fullPath = System.IO.Path.Combine(homePath, stlFile);
                    if (System.IO.File.Exists(fullPath))
                    {
                        zipArchive.CreateEntryFromFile(fullPath, stlFile);
                    }
                }
            }

        }





        /// <summary>
        /// The Exposure property controls where in the panel a component icon 
        /// will appear. There are seven possible locations (primary to septenary), 
        /// each of which can be combined with the GH_Exposure.obscure flag, which 
        /// ensures the component will only be visible on panel dropdowns.
        /// </summary>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// You can add image files to your project resources and access them like this:
        /// return Resources.IconForThisComponent;
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid => GuidUtility.CreateDeterministicGuid("component2");
    }
}
