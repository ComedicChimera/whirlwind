using System;

using Whirlwind.PackageManager;

namespace Whirlwind
{
    class Program
    {
        public static Compiler compiler;
        public static PackageManager.PackageManager packageManager;

        // TODO - reconfigure to use command line args for the final build
        // TODO - change directory to whirlpath environment variable
        static void Main(string[] args)
        {
            string fileName = Console.ReadLine();

            if (packageManager.OpenPackage(fileName, out string text)) {
                compiler = new Compiler("config/tokens.json", "config/grammar.ebnf");
                compiler.Build(text);

                Console.ReadKey();
            }
            else
            {
                Console.WriteLine($"Unable to open file at \'{fileName}\'.");
                Console.ReadKey();
            }
        }
    }
}