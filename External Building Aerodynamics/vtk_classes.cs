using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kitware.VTK;
using Rhino.Geometry;

namespace External_Building_Aerodynamics
{
    public class MeshInterpolator
    {
        private string _comfortPlotPath;

        public string ComfortPlotPath
        {
            get { return _comfortPlotPath; }
            set
            {
                // Your custom logic before setting the value
                if (!string.IsNullOrWhiteSpace(value) && File.Exists(value))
                {
                    _comfortPlotPath = value;
                }
                else
                {
                    throw new FileNotFoundException($"The file at {value} was not found.");
                }
            }
        }

        private List<string> _windDirectionPaths;

        public List<string> WindDirectionPaths
        {
            get { return _windDirectionPaths; }
            set
            {
                // Check if the list is not null
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value), "WindDirectionPaths cannot be set to null.");
                }

                // Verify each file path in the list
                foreach (var path in value)
                {
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    {
                        throw new FileNotFoundException($"The file at {path} was not found.");
                    }
                }

                _windDirectionPaths = value;
            }
        }

        public vtkMultiBlockDataSet FilterBlocksByName(vtkMultiBlockDataSet dataset, string keyword)
        {
            if (dataset == null)
                return null;

            var filteredBlocks = vtkMultiBlockDataSet.New();

            uint numBlocks = dataset.GetNumberOfBlocks();

            for (uint i = 0; i < numBlocks; i++)
            {
                var blockName = dataset.GetMetaData(i).Get(vtkCompositeDataSet.NAME());
                if (blockName.Contains(keyword))
                {
                    filteredBlocks.SetBlock(i, dataset.GetBlock(i));
                }
            }

            return filteredBlocks;
        }

        public void RenameFieldsWithDirection(vtkMultiBlockDataSet dataset, string direction)
        {
            for (uint i = 0; i < dataset.GetNumberOfBlocks(); i++)
            {
                var block = dataset.GetBlock(i) as vtkDataSet;
                if (block == null) continue;

                for (int j = 0; j < block.GetPointData().GetNumberOfArrays(); j++)
                {
                    var array = block.GetPointData().GetArray(j);
                    array.SetName(array.GetName() + "__" + direction);
                }
            }
        }

        public string ProcessDatasets(string field, string outputPath)
        {
            Console.WriteLine(ComfortPlotPath);
            // 1. Read and filter comfort plot
            var comfortPlotReader = vtkXMLMultiBlockDataReader.New();
            comfortPlotReader.SetFileName(ComfortPlotPath);
            comfortPlotReader.Update();
            var comfortData = comfortPlotReader.GetOutput() as vtkMultiBlockDataSet;

            var filteredBlocks = FilterBlocksByName(comfortData, "Pedestrian level");

            // 2. Read and filter wind direction results
            List<vtkMultiBlockDataSet> windDirectionDatasets = new List<vtkMultiBlockDataSet>();

            foreach (var windDirectionPath in WindDirectionPaths)
            {
                var windDirectionReader = vtkEnSightGoldBinaryReader.New();
                windDirectionReader.SetCaseFileName(windDirectionPath);
                windDirectionReader.Update();
                var windData = windDirectionReader.GetOutput() as vtkMultiBlockDataSet;
                windData = IterateArraysInMultiBlock(windData, field);
                var direction = Directory.GetParent(Path.GetDirectoryName(windDirectionPath)).Name;

                var filteredWindDirectionData = FilterBlocksByName(windData, "wind_comfort_surface");

                RenameFieldsWithDirection(filteredWindDirectionData, direction);

                windDirectionDatasets.Add(filteredWindDirectionData);
            }

            // 3. Resample wind directions onto the comfort plot mesh
            int iteration = 0;

            foreach (var windDirectionDataset in windDirectionDatasets)
            {
                var resampler = vtkResampleWithDataSet.New();
                resampler.SetInputData(filteredBlocks);
                resampler.SetSourceData(windDirectionDataset);

                if (iteration != 0)
                {
                    resampler.SetPassPointArrays(true);
                }

                resampler.Update();

                filteredBlocks.DeepCopy(resampler.GetOutput());

                iteration++;
            }
            RemoveUnwantedArraysFromMultiBlock(filteredBlocks);
            var mergedDataset = MergeUnstructuredGridBlocks(filteredBlocks);
            if (mergedDataset == null)
            {
                Console.WriteLine("Error: Failed to merge the dataset.");
                return null;
            }
            var speedUpFactors = ComputeSpeedUpFactors(mergedDataset);

            // Check if the directory exists
            if (!Directory.Exists(outputPath))
            {
                // If it doesn't exist, create the directory
                Directory.CreateDirectory(outputPath);
            }

            ExportToVTU(mergedDataset, Path.Combine(outputPath, "velocity.vtu"));
            ExportToVTU(speedUpFactors, Path.Combine(outputPath, "speed_up_factors.vtu"));


            return Path.Combine(outputPath, "speed_up_factors.vtu");
        }

        public vtkMultiBlockDataSet IterateArraysInMultiBlock(vtkMultiBlockDataSet multiBlock, string keyword)
        {
            if (multiBlock == null)
                return null;

            uint numBlocks = multiBlock.GetNumberOfBlocks();
            for (uint i = 0; i < numBlocks; i++)
            {
                var currentBlock = multiBlock.GetBlock(i);
                if (currentBlock is vtkDataSet)
                {
                    vtkDataSet dataSetBlock = currentBlock as vtkDataSet;
                    vtkPointData pointData = dataSetBlock.GetPointData();

                    if (pointData != null)
                    {
                        int numArrays = pointData.GetNumberOfArrays();
                        for (int j = numArrays - 1; j >= 0; j--)
                        {
                            vtkDataArray dataArray = pointData.GetArray(j);
                            if (dataArray != null)
                            {
                                string arrayName = dataArray.GetName();
                                if (!arrayName.StartsWith(keyword))
                                {
                                    pointData.RemoveArray(arrayName);
                                    Console.WriteLine($"Removed array: {arrayName}");
                                }
                            }
                        }
                    }
                }
            }
            return multiBlock;
        }

        public void ExportToVTU(vtkUnstructuredGrid dataset, string outputPath)
        {
            var writer = vtkXMLUnstructuredGridWriter.New();
            writer.SetFileName(outputPath);
            writer.SetInputData(dataset);
            writer.Write();
        }


        public void RemoveUnwantedArraysFromDataSet(vtkDataSet dataSet)
        {
            if (dataSet == null) return;

            vtkPointData pointData = dataSet.GetPointData();
            if (pointData != null)
            {
                int numPointArrays = pointData.GetNumberOfArrays();
                for (int i = numPointArrays - 1; i >= 0; i--)
                {
                    string arrayName = pointData.GetArrayName(i);
                    if (arrayName.StartsWith("vtk"))
                    {
                        pointData.RemoveArray(arrayName);
                    }
                }
            }

            vtkCellData cellData = dataSet.GetCellData();
            if (cellData != null)
            {
                int numCellArrays = cellData.GetNumberOfArrays();
                for (int i = numCellArrays - 1; i >= 0; i--)
                {
                    string arrayName = cellData.GetArrayName(i);
                    if (arrayName.StartsWith("vtk"))
                    {
                        cellData.RemoveArray(arrayName);
                    }
                }
            }
        }

        public void RemoveUnwantedArraysFromMultiBlock(vtkMultiBlockDataSet multiBlock)
        {
            if (multiBlock == null)
                return;

            uint numBlocks = multiBlock.GetNumberOfBlocks();
            for (uint i = 0; i < numBlocks; i++)
            {
                var currentBlock = multiBlock.GetBlock(i);

                if (currentBlock is vtkDataSet)
                {
                    vtkDataSet dataSetBlock = currentBlock as vtkDataSet;
                    RemoveUnwantedArraysFromDataSet(dataSetBlock);
                }
                else if (currentBlock is vtkMultiBlockDataSet)
                {
                    RemoveUnwantedArraysFromMultiBlock(currentBlock as vtkMultiBlockDataSet);
                }
            }
        }

        public vtkUnstructuredGrid MergeUnstructuredGridBlocks(vtkMultiBlockDataSet multiBlock)
        {
            if (multiBlock == null)
                return null;

            var appendFilter = vtkAppendFilter.New();

            bool validBlockFound = false;

            Action<vtkDataObject> processBlock = null;
            processBlock = (currentBlock) =>
            {
                if (currentBlock is vtkUnstructuredGrid)
                {
                    validBlockFound = true;
                    appendFilter.AddInputData(currentBlock as vtkUnstructuredGrid);
                }
                else if (currentBlock is vtkMultiBlockDataSet)
                {
                    vtkMultiBlockDataSet nestedBlock = currentBlock as vtkMultiBlockDataSet;
                    uint nestedNumBlocks = nestedBlock.GetNumberOfBlocks();
                    for (uint nestedIndex = 0; nestedIndex < nestedNumBlocks; nestedIndex++)
                    {
                        var nestedCurrentBlock = nestedBlock.GetBlock(nestedIndex);
                        if (nestedCurrentBlock != null)
                        {
                            processBlock(nestedCurrentBlock);
                        }
                    }
                }
            };

            uint numBlocks = multiBlock.GetNumberOfBlocks();
            for (uint i = 0; i < numBlocks; i++)
            {
                var currentBlock = multiBlock.GetBlock(i);
                if (currentBlock != null)
                {
                    Console.WriteLine($"Processing Block {i} of type: {currentBlock.GetClassName()}");
                    processBlock(currentBlock);
                }
                else
                {
                    Console.WriteLine($"Block {i} is null.");
                }
            }

            if (!validBlockFound)
            {
                Console.WriteLine("No valid vtkUnstructuredGrid blocks found.");
                return null;
            }

            appendFilter.Update();
            return appendFilter.GetOutput();
        }

        public vtkUnstructuredGrid ComputeSpeedUpFactors(vtkUnstructuredGrid inputDataset)
        {
            var pointData = inputDataset.GetPointData();
            int numArrays = pointData.GetNumberOfArrays();

            for (int i = 0; i < numArrays; i++)
            {
                var arrayName = pointData.GetArrayName(i);

                if (arrayName.StartsWith("Velocity_n"))
                {
                    var parts = arrayName.Split(new[] { "__" }, StringSplitOptions.None);
                    var suffix = parts.Length > 1 ? parts[1] : "";

                    var tempArrayName = "TempVelocityArray";
                    pointData.GetArray(arrayName).SetName(tempArrayName);  // Temporarily rename the array

                    var calculator = vtkArrayCalculator.New();
                    calculator.SetInputData(inputDataset);
                    calculator.AddScalarVariable(tempArrayName, tempArrayName, 0);  // Set the alias for the array
                                                                                    // Set the alias for the array
                    calculator.SetFunction($"{tempArrayName} / 10");

                    var resultArrayName = suffix;
                    calculator.SetResultArrayName(resultArrayName);

                    calculator.Update();

                    inputDataset = calculator.GetOutput() as vtkUnstructuredGrid;

                    // Remove the original array
                    inputDataset.GetPointData().RemoveArray(tempArrayName);
                }
            }

            return inputDataset;
        }

        public string createSpeedUpFactors(List<string> directionPaths,
            string ComfortPlotPath, string outputPath)
        {
            // Create a MeshInterpolator instance
            MeshInterpolator interpolator = new MeshInterpolator();

            // Setting the ComfortPlotPath (You should replace this with your actual path)
            interpolator.ComfortPlotPath = ComfortPlotPath;

            // Setting up WindDirectionPaths (Replace these with your actual paths)
            interpolator.WindDirectionPaths = directionPaths;

            return interpolator.ProcessDatasets("Velocity_n", outputPath);
        }

        public class singleSpeed
        {
            private string inputFilePath;
            private double windDirection;
            private double speedMultiplier;
            private string outputFilePath;

            public singleSpeed(string inputFilePath, double windDirection, double speed)
            {
                this.inputFilePath = inputFilePath;
                this.windDirection = windDirection;
                this.speedMultiplier = speed;
                this.outputFilePath = Path.Combine(Path.GetDirectoryName(inputFilePath), "single_speed.vtu");
            }

            public string ProcessVTU()
            {
                // Load the VTU file
                var reader = vtkXMLUnstructuredGridReader.New();
                reader.SetFileName(inputFilePath);
                reader.Update();

                vtkUnstructuredGrid data = reader.GetOutput();

                // Round wind direction and select nearest field
                string selectedFieldName = SelectNearestField(data, windDirection);

                // Multiply the field values
                MultiplyFieldValues(data, selectedFieldName, speedMultiplier);

                // Write to a new VTU file
                vtkXMLUnstructuredGridWriter writer = vtkXMLUnstructuredGridWriter.New();
                writer.SetFileName(outputFilePath);
                writer.SetInputData(data);
                writer.Write();

                return outputFilePath;
            }

            private string SelectNearestField(vtkUnstructuredGrid data, double direction)
            {
                // Assuming fields are named after their angles, e.g., "0", "45", "90", etc.
                // You'll need to replace this with the actual way fields are named in your dataset
                List<double> availableAngles = new List<double>();

                // Populate availableAngles with the angles from the dataset fields
                for (int i = 0; i < data.GetPointData().GetNumberOfArrays(); i++)
                {
                    string fieldName = data.GetPointData().GetArrayName(i);
                    if (double.TryParse(fieldName, out double angle))
                    {
                        availableAngles.Add(angle);
                    }
                }

                // Normalize the direction to be within [0, 360)
                direction = direction % 360;
                if (direction < 0) direction += 360;

                // Find the nearest angle
                double nearestAngle = availableAngles.OrderBy(a => Math.Abs(direction - a)).First();

                // Return the corresponding field name
                return nearestAngle.ToString();
            }


            private void MultiplyFieldValues(vtkUnstructuredGrid data, string arrayName, double multiplier)
            {
                // Access the field from the data
                var pointData = data.GetPointData();
                var originalArray = pointData.GetArray(arrayName);

                if (originalArray != null)
                {
                    var tempArrayName = "TempSpeedArray";
                    originalArray.SetName(tempArrayName);  // Temporarily rename the array

                    var calculator = vtkArrayCalculator.New();
                    calculator.SetInputData(data);
                    calculator.AddScalarVariable("tempVar", tempArrayName, 0);  // Set the alias for the array
                    calculator.SetFunction($"tempVar * {multiplier}");
                    calculator.SetResultArrayName("Speed (m/s)");
                    calculator.Update();

                    // Replace the data with the calculator's output
                    data.DeepCopy(calculator.GetOutput());

                    // Remove the temporary array
                    //data.GetPointData().RemoveArray(tempArrayName);

                    RetainOnlyTargetField(data, "Speed (m/s)");
                }
                else
                {
                    throw new ArgumentException($"Field '{arrayName}' not found in the dataset.");
                }
            }

            public void RetainOnlyTargetField(vtkUnstructuredGrid dataset, string targetFieldName)
            {
                if (dataset == null)
                    return;

                vtkPointData pointData = dataset.GetPointData();
                if (pointData != null)
                {
                    int numArrays = pointData.GetNumberOfArrays();
                    for (int j = numArrays - 1; j >= 0; j--)
                    {
                        vtkDataArray dataArray = pointData.GetArray(j);
                        if (dataArray != null)
                        {
                            string arrayName = dataArray.GetName();
                            if (arrayName != targetFieldName)
                            {
                                pointData.RemoveArray(arrayName);
                                Console.WriteLine($"Removed array: {arrayName}");
                            }
                        }
                    }
                }
            }

        }

        public class VTUToRhinoMesh
        {
            public static (Mesh, List<double>) ConvertVTUToRhinoMesh(string vtuFilePath, string dataFieldName)
            {
                // Read the VTU file
                var reader = vtkXMLUnstructuredGridReader.New();
                reader.SetFileName(vtuFilePath);
                reader.Update();

                vtkUnstructuredGrid unstructuredGrid = reader.GetOutput();

                // Convert to a Rhino mesh
                Mesh rhinoMesh = new Mesh();
                List<double> dataValues = new List<double>();

                // Extract points and corresponding data values
                for (int i = 0; i < unstructuredGrid.GetNumberOfPoints(); i++)
                {
                    double[] point = unstructuredGrid.GetPoint(i);
                    rhinoMesh.Vertices.Add(new Point3d(point[0], point[1], point[2]));

                    // Extract data value for this point
                    vtkDataArray dataArray = unstructuredGrid.GetPointData().GetArray(dataFieldName);
                    if (dataArray != null)
                    {
                        double dataValue = dataArray.GetComponent(i, 0); // Assuming scalar data
                        dataValues.Add(dataValue);
                    }
                    else
                    {
                        // Handle missing data appropriately
                        dataValues.Add(0.0); // Example: default to 0 if data is missing
                    }
                }

                // Extract cells (faces)
                for (int i = 0; i < unstructuredGrid.GetNumberOfCells(); i++)
                {
                    vtkCell cell = unstructuredGrid.GetCell(i);
                    if (cell.GetCellType() == 5) // VTK_TRIANGLE
                    {
                        vtkIdList pointIds = cell.GetPointIds();
                        rhinoMesh.Faces.AddFace((int)pointIds.GetId(0), (int)pointIds.GetId(1), (int)pointIds.GetId(2));
                    }
                    // Handle other cell types as needed
                }

                rhinoMesh.Normals.ComputeNormals();
                rhinoMesh.Compact();

                return (rhinoMesh, dataValues);
            }
        }

        public static class VTUNames
        {
            public static List<string> GetArrayNamesFromVTUFile(string vtuFilePath)
            {
                List<string> arrayNames = new List<string>();

                var reader = vtkXMLUnstructuredGridReader.New();
                reader.SetFileName(vtuFilePath);
                reader.Update();
                vtkUnstructuredGrid data = reader.GetOutput();

                if (data != null)
                {
                    var pointData = data.GetPointData();
                    for (int i = 0; i < pointData.GetNumberOfArrays(); i++)
                    {
                        string arrayName = pointData.GetArray(i).GetName();
                        arrayNames.Add(arrayName);
                    }
                }

                // Separate numeric and non-numeric names
                var numericNames = arrayNames
                    .Where(name => double.TryParse(name, out _))
                    .Select(name => new { Original = name, Number = double.Parse(name) });

                var nonNumericNames = arrayNames
                    .Where(name => !double.TryParse(name, out _));

                // Sort numeric names and format them
                var sortedFormattedNumericNames = numericNames
                    .OrderBy(n => n.Number)
                    .Select(n => n.Number.ToString("0.0"))
                    .ToList();

                // Combine numeric and non-numeric names, keeping the original order for non-numeric
                return sortedFormattedNumericNames.Concat(nonNumericNames).ToList();
            }
        }
        public static class meshReduction
        {
            public static void ReduceMeshResolution(string inputFilePath, string outputFilePath, double reductionFactor)
            {
                // Read the VTU file
                var reader = vtkXMLUnstructuredGridReader.New();
                reader.SetFileName(inputFilePath);
                reader.Update();

                vtkUnstructuredGrid originalGrid = reader.GetOutput();

                // Convert Unstructured Grid to PolyData
                var geometryFilter = vtkGeometryFilter.New();
                geometryFilter.SetInputConnection(reader.GetOutputPort());
                geometryFilter.Update();

                // Apply Decimation
                var decimate = vtkDecimatePro.New();
                decimate.SetInputConnection(geometryFilter.GetOutputPort());
                decimate.SetTargetReduction(reductionFactor); // e.g., 0.5 for 50% reduction
                decimate.Update();

                // Convert Decimated PolyData Back to UnstructuredGrid
                vtkPolyData polyData = decimate.GetOutput();
                vtkUnstructuredGrid newUnstructuredGrid = vtkUnstructuredGrid.New();
                newUnstructuredGrid.SetPoints(polyData.GetPoints());

                // Transfer point data
                newUnstructuredGrid.GetPointData().ShallowCopy(originalGrid.GetPointData());

                for (int i = 0; i < polyData.GetNumberOfCells(); i++)
                {
                    vtkCell cell = polyData.GetCell(i);
                    newUnstructuredGrid.InsertNextCell(cell.GetCellType(), cell.GetPointIds());
                }

                // Transfer cell data
                // This step is more complex and might need special handling based on your data

                // Write the New UnstructuredGrid to a VTU File
                var writer = vtkXMLUnstructuredGridWriter.New();
                writer.SetFileName(outputFilePath);
                writer.SetInputData(newUnstructuredGrid);
                writer.Write();
            }


        }


    }
}