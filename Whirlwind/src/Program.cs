﻿using System;

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

            compiler = new Compiler("config/tokens.json", "config/grammar.ebnf");
            packageManager = new PackageManager.PackageManager();

            if (!packageManager.ImportRaw(fileName))
                Console.WriteLine($"Unable to open file at \'{fileName}\'.");

            Console.ReadKey();
        }
    }
}