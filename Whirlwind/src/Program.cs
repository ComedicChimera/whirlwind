using System;
using System.Collections.Generic;
using System.Linq;

namespace Whirlwind
{
    class Program
    {
        public static Compiler compiler;

        // TODO - reconfigure to use command line args for the final build
        // TODO - change directory to whirlpath environment variable
        static void Main(string[] args)
        {
            _initVars();

            string mainPath = Console.ReadLine();

            compiler = new Compiler("config/tokens.json", "config/grammar.ebnf");

            if (!compiler.Build(mainPath))
                Console.WriteLine($"Unable to open package at {mainPath}");

            Console.ReadKey();
        }

        static void _initVars()
        {
            WhirlGlobals.WHIRL_PATH = "C:/Users/forlo/dev/Projects/whirlwind/compiler/Whirlwind/";
        }
    }
}