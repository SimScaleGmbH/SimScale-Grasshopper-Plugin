using System;

namespace Installer
{
    class Program
    {
        static void Main(string[] args)
        {
            string srcFolder = AppDomain.CurrentDomain.BaseDirectory;

            executable_classes.UnblockAndMoveFiles(srcFolder);

            Console.WriteLine();
            Console.WriteLine("Done. Press any key to close this window.");
            Console.ReadKey();
        }
    }
}