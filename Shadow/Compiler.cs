using System;
using Shadow.Parser;

namespace Shadow
{
    class Compiler
    {
        private Scanner _scanner;
        private ShadowParser _parser;

        public Compiler(string tokenPath, string grammarPath)
        {
            _scanner = new Scanner(tokenPath);

            var gramloader = new GramLoader();
            _parser = new ShadowParser(gramloader.Load(grammarPath));

        }

        public void Build(string text)
        {
            var tokens = _scanner.Scan(text);

            ASTNode ast;
            try
            {
                ast = _parser.Parse(tokens);
            }
            catch (InvalidSyntaxException isx)
            {
                Console.WriteLine($"Unexpected Token: \'{isx.Tok.Value}\' at {isx.Tok.Index}");
                return;
            }
            Console.WriteLine(ast.ToString());
        }
    }
}
