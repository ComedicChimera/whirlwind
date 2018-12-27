using System;
using System.Linq;

using Whirlwind.Parser;
using Whirlwind.Semantic.Visitor;
using Whirlwind.Semantic;

namespace Whirlwind
{
    class Compiler
    {
        private Scanner _scanner;
        private WhirlParser _parser;

        public Compiler(string tokenPath, string grammarPath)
        {
            _scanner = new Scanner(tokenPath);

            var gramloader = new GramLoader();
            _parser = new WhirlParser(gramloader.Load(grammarPath));

        }

        public void Build(string text)
        {
            var st = new SymbolTable();
            Build(text, ref st);
        }

        public void Build(string text, ref SymbolTable table)
        {
            var tokens = _scanner.Scan(text);

            ASTNode ast;
            try
            {
                ast = _parser.Parse(tokens);
            }
            catch (InvalidSyntaxException isx)
            {
                if (isx.Tok.Type == "EOF")
                    Console.WriteLine("Unexpected End of File");
                else
                {
                    WriteError(text, $"Unexpected Token: '{isx.Tok.Value}'", isx.Tok.Index, isx.Tok.Value.Length);
                }
                return;
            }
            // Console.WriteLine(ast.ToString() + "\n");

            var visitor = new Visitor();

            try
            {
                visitor.Visit(ast);
            }
            catch (SemanticException smex)
            {
                WriteError(text, smex.Message, smex.Position.Start, smex.Position.Length);
                return;
            }

            if (visitor.ErrorQueue.Count > 0)
            {
                foreach (var error in visitor.ErrorQueue)
                    WriteError(text, error.Message, error.Position.Start, error.Position.Length);

                return;
            }

            table = visitor.Table();
            Console.WriteLine(visitor.Result().ToString());
        }

        private void WriteError(string text, string message, int position, int length)
        {
            int line = GetLine(text, position), column = GetColumn(text, position);
            Console.WriteLine($"{message} at (Line: {line + 1}, Column: {column})");
            Console.WriteLine($"\n\t{text.Split('\n')[line].Trim('\t')}");
            Console.WriteLine("\t" + String.Concat(Enumerable.Repeat(" ", column - 1)) + String.Concat(Enumerable.Repeat("^", length)));
        }

        private int GetLine(string text, int ndx)
        {
            return text.Substring(0, ndx + 1).Count(x => x == '\n');
        }

        private int GetColumn(string text, int ndx)
        {
            var splitText = text.Substring(0, ndx + 1).Split('\n');
            return splitText[splitText.Count() - 1].Trim('\t').Length;
        }
    }
}
