using System;
using System.IO;

namespace Shadow
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

            Compiler compiler = new Compiler();
            compiler.Build(text);
            
        }
    }
}