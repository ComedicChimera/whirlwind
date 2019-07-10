using System;

namespace Whirlwind
{
    class Program
    {
        public static PackageManager packageManager;

        // TODO - reconfigure to use command line args for the final build
        // TODO - change directory to whirlpath environment variable
        static void Main(string[] args)
        {
            string fileName = Console.ReadLine();

            packageManager = new PackageManager();

            if (!packageManager.ImportRaw(fileName))
                Console.WriteLine($"Unable to open file at \'{fileName}\'.");

            Console.ReadKey();
        }
    }
}