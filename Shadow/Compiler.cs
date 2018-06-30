using System;
using Shadow.Parser;

namespace Shadow
{
    class Compiler
    {
        private Scanner _scanner = new Scanner("Config/tokens.json");

        public void Build(string text)
        {
            var tokens = _scanner.Scan(text);

            foreach (var token in tokens)
            {
                Console.WriteLine($"({token.Type}, {token.Value}) at {token.Index}");
            }

            Console.ReadKey();
        }
    }
}
