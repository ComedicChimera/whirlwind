using System;
using System.Collections.Generic;
using System.Linq;

namespace Whirlwind
{
    class Program
    {
        public static Compiler WhirlCompiler;

        private static string[] _validCommands = { "build", "fetch", "del", "update", "config", "run", "test", "load" };

        static void Main(string[] args)
        {
            _initVars();

            if (args.Length == 0)
                _showHelp();
            else
            {
                var unnamedArgs = new List<string>();
                var namedArgs = new Dictionary<string, string>();

                string prevArg = "";
                foreach (var arg in args.Skip(1))
                {
                    if (arg.StartsWith('-'))
                    {
                        prevArg = arg.TrimStart('-');
                        namedArgs.Add(prevArg, "");
                    }
                    else if (prevArg == "")
                        unnamedArgs.Add(arg);
                    else
                        // we know prevArg is in dictionary
                        namedArgs[prevArg] = arg;
                }

                string command = args[0];

                if (!_validCommands.Contains(command))
                {
                    Console.WriteLine($"Unknown command `{command}`");
                    _showHelp();
                }
                else if (!_processArgs(command, unnamedArgs, namedArgs))
                {
                    Console.WriteLine($"Invalid use of command `{command}`");
                    _showHelp(command);
                }
            }

            Console.ReadKey();
        }

        static bool _processArgs(string command, List<string> unnamedArgs, Dictionary<string, string> namedArgs)
        {
            switch (command)
            {
                case "build":
                    {
                        if (unnamedArgs.Count != 1)
                            return false;

                        WhirlCompiler = new Compiler("config/tokens.json", "config/grammar.ebnf");

                        // add flag parsing here

                        WhirlCompiler.Build(unnamedArgs[0]);
                    }
                    break;
            }

            return true;
        }

        static void _initVars()
        {
            // TODO - get whirl path from env
            WhirlGlobals.WHIRL_PATH = "C:/Users/forlo/dev/Projects/whirlwind/compiler/Whirlwind/";
        }

        static void _showHelp()
        {
            Console.WriteLine("Whirlwind CLI Overview:\n");

            foreach (var cmd in _validCommands)
                _showHelp(cmd, "\t");

            Console.WriteLine("\nFor more help, look at:");
            Console.WriteLine("* Using Your Compiler (www.whirlwind-lang.org/docs/compiler-guide)");
            Console.WriteLine("* CLI Reference (www.whirlwind-lang.org/docs/cli-reference)");
        }

        static void _showHelp(string command, string tabPrefix = "")
        {

        }
    }
}