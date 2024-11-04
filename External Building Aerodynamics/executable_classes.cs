using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace External_Building_Aerodynamics
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
        public static void UnblockAndMoveFiles(string srcFolder, string destFolder)
        {
            if (!Directory.Exists(srcFolder))
            {
                Console.WriteLine($"Source folder does not exist: {srcFolder}");
                return;
            }

            Directory.CreateDirectory(destFolder);

            foreach (var file in Directory.GetFiles(srcFolder))
            {
                UnblockFile(file);
                string destPath = Path.Combine(destFolder, Path.GetFileName(file));
                File.Move(file, destPath);
                Console.WriteLine($"Moved {file} to {destPath} and unblocked it");
            }
        }
    }
}
