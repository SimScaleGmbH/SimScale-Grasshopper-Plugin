using System;
using System.Collections.Generic;
using System.IO;
using Kitware.VTK;

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
                    this.outputFilePath = Path.Combine(Path.GetDirectoryName(inputFilePath), "temp_output.vtu");
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
                    // Logic to round the direction and select the nearest field
                    // Placeholder - implement based on your specific needs
                    return "nearest_field_name";
                }

                private void MultiplyFieldValues(vtkUnstructuredGrid data, string fieldName, double multiplier)
                {
                    // Logic to multiply the field values
                    // Placeholder - implement based on your specific needs
                }
            }


        }
}