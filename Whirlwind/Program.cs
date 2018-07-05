using System;
using System.IO;

namespace Whirlwind
{
    class Program
    {
        // reconfigure to use command line args for the final build
        static void Main(string[] args)
        {
            string fileName = Console.ReadLine();
            string text;
            try
            {
                text = File.ReadAllText(fileName);
            } catch (FileNotFoundException)
            {
                Console.WriteLine($"Unable to open file at \'{fileName}\'.");
                Console.ReadKey();
                return;
            }

            Compiler compiler = new Compiler("Config/tokens.json", "Config/grammar.ebnf");
            compiler.Build(text);

            Console.ReadKey();
        }
    }
}