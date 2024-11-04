using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Installer
{
    internal class executable_classes
    {
        // Import DeleteFile from kernel32.dll to remove the Zone.Identifier stream.
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeleteFile(string name);

        // Method to unblock a file by deleting the Zone.Identifier
        public static void UnblockFile(string filePath)
        {
            string zoneIdentifier = filePath + ":Zone.Identifier";
            if (File.Exists(zoneIdentifier))
            {
                DeleteFile(zoneIdentifier);
                Console.WriteLine($"Unblocked {filePath}");
            }
        }

        // Method to unblock and move files from a source to a destination directory
        public static void UnblockAndMoveFiles(string srcFolder)
        {
            // Define the source folder path to include "src" subdirectory
            string sourcePath = Path.Combine(srcFolder, "src");

            // Define the destination folder path
            string grasshopperLibrariesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Grasshopper",
                "Libraries"
            );

            // Check if "Libraries" exists; if not, throw an error
            if (!Directory.Exists(grasshopperLibrariesPath))
            {
                Console.WriteLine($"Error: Libraries folder does not exist: {grasshopperLibrariesPath}");
                return;
            }

            // Define the full path including "SimScale"
            string destFolder = Path.Combine(grasshopperLibrariesPath, "SimScale");

            // Create "SimScale" if it does not exist
            if (!Directory.Exists(destFolder))
            {
                Console.WriteLine($"Creating SimScale directory at: {destFolder}");
                Directory.CreateDirectory(destFolder);
            }
            else
            {
                Console.WriteLine($"Destination folder exists: {destFolder}");
            }

            // Ensure the "src" source folder exists
            if (!Directory.Exists(sourcePath))
            {
                Console.WriteLine($"Source 'src' folder does not exist: {sourcePath}");
                return;
            }
            else
            {
                Console.WriteLine($"Source 'src' folder found: {sourcePath}");
            }

            // Proceed to unblock and move files from "src" subdirectory
            foreach (var file in Directory.GetFiles(sourcePath))
            {
                Console.WriteLine($"Processing file: {file}");
                UnblockFile(file);

                string destPath = Path.Combine(destFolder, Path.GetFileName(file));
                File.Move(file, destPath);
                Console.WriteLine($"Moved {file} to {destPath} and unblocked it");
            }

            Console.WriteLine("File moving and unblocking completed.");
        }





    }
}
