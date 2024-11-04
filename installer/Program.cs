using System;

namespace Installer
{
    class Program
    {
        static void Main(string[] args)
        {
            string srcFolder = AppDomain.CurrentDomain.BaseDirectory;

            executable_classes.UnblockAndMoveFiles(srcFolder);
        }
    }
}