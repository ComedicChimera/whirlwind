using System;
using System.Linq;
using System.Collections.Generic;

using Whirlwind.Syntax;
using Whirlwind.Semantic.Visitor;
using Whirlwind.Semantic;
using Whirlwind.Generation;

namespace Whirlwind
{
    class Compiler
    {
        private Scanner _scanner;
        private Syntax.Parser _parser;

        public Compiler(string tokenPath, string grammarPath)
        {
            _scanner = new Scanner(tokenPath);

            var gramloader = new GramLoader();
            _parser = new Syntax.Parser(gramloader.Load(grammarPath));

        }

        public void Build(string text, string namePrefix, ref Dictionary<string, Symbol> table)
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

            
            var visitor = new Visitor(namePrefix, false);

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

            var fullTable = visitor.Table();

            var generator = new Generator(fullTable, visitor.Flags());

            try
            {
                // supplement in real file name when appropriate
                generator.Generate(visitor.Result(), "test.llvm");
            }
            catch (GeneratorException ge)
            {
                Console.WriteLine("Generation Error: " + ge.ErrorMessage);
            }
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
