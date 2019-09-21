using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using Whirlwind.Syntax;
using Whirlwind.Semantic.Visitor;
using Whirlwind.Semantic;
using Whirlwind.Generation;
using Whirlwind.Types;

using static Whirlwind.WhirlGlobals;

namespace Whirlwind
{
    class Compiler
    {
        private Scanner _scanner;
        private Parser _parser;

        private bool _compiledMainFile;

        public Compiler(string tokenPath, string grammarPath)
        {
            _scanner = new Scanner(WHIRL_PATH + tokenPath);

            var gramloader = new GramLoader();
            _parser = new Parser(gramloader.Load(WHIRL_PATH + grammarPath));

            _compiledMainFile = false;
        }

        public void Build(string text, string namePrefix, ref Dictionary<string, Symbol> table)
        {
            bool isMainFile;

            if (!_compiledMainFile)
            {
                isMainFile = true;
                _compiledMainFile = true;
            }
            else
                isMainFile = false;

            var tokens = _scanner.Scan(text);

            ASTNode ast = _runParser(_parser, tokens, text);

            if (ast == null)
                return;
            
            var visitor = new Visitor(namePrefix, false);
            _runVisitor(visitor, ast, text);

            var fullTable = visitor.Table();
            var sat = visitor.Result();

            if (isMainFile)
            {
                if (!fullTable.Lookup("main", out Symbol symbol))
                {
                    Console.WriteLine("Main file missing main function definition");
                    return;
                }

                if (symbol.DataType.Classify() != TypeClassifier.FUNCTION)
                {
                    Console.WriteLine("Symbol `main` in main file must be a function");
                    return;
                }

                var userMainDefinition = (FunctionType)symbol.DataType;

                if (!_generateUserMainCall(userMainDefinition, out string mainUserCall))
                {
                    Console.WriteLine("Invalid main function declaration");
                    return;
                }

                var mainTemplate = File.ReadAllText(WHIRL_PATH + "lib/std/__core__/main.wrl");

                var mtTokens = _scanner.Scan(mainTemplate);

                var mtAst = _runParser(_parser, mtTokens, mainTemplate);

                if (mtAst == null)
                    return;

                var mtVisitor = new Visitor("", false);
                _runVisitor(mtVisitor, mtAst, mainTemplate);

                foreach (var item in mtVisitor.Table().GetScope())
                {
                    if (!fullTable.AddSymbol(item))
                    {
                        if (item.Name.StartsWith("__"))
                        {
                            Console.WriteLine("Use of reserved name in main file");
                            return;
                        }                           
                    }
                }

                ((BlockNode)sat).Block.AddRange(((BlockNode)mtVisitor.Result()).Block);
            }

            var generator = new Generator(fullTable, visitor.Flags());

            try
            {
                // supplement in real file name when appropriate
                generator.Generate(sat, "test.llvm");
            }
            catch (GeneratorException ge)
            {
                Console.WriteLine("Generation Error: " + ge.ErrorMessage);
            }
        }

        private void _writeError(string text, string message, int position, int length)
        {
            int line = _getLine(text, position), column = _getColumn(text, position);
            Console.WriteLine($"{message} at (Line: {line + 1}, Column: {column})");
            Console.WriteLine($"\n\t{text.Split('\n')[line].Trim('\t')}");
            Console.WriteLine("\t" + String.Concat(Enumerable.Repeat(" ", column - 1)) + String.Concat(Enumerable.Repeat("^", length)));
        }

        private int _getLine(string text, int ndx)
        {
            return text.Substring(0, ndx + 1).Count(x => x == '\n');
        }

        private int _getColumn(string text, int ndx)
        {
            var splitText = text.Substring(0, ndx + 1).Split('\n');
            return splitText[splitText.Count() - 1].Trim('\t').Length;
        }

        private ASTNode _runParser(Parser parser, List<Token> tokens, string text)
        {
            try
            {
                return _parser.Parse(tokens);
            }
            catch (InvalidSyntaxException isx)
            {
                if (isx.Tok.Type == "EOF")
                    Console.WriteLine("Unexpected End of File");
                else
                {
                    _writeError(text, $"Unexpected Token: '{isx.Tok.Value}'", isx.Tok.Index, isx.Tok.Value.Length);
                }
                return null;
            }
        }

        private void _runVisitor(Visitor visitor, ASTNode ast, string text)
        {
            try
            {
                visitor.Visit(ast);
            }
            catch (SemanticException smex)
            {
                _writeError(text, smex.Message, smex.Position.Start, smex.Position.Length);
                return;
            }

            if (visitor.ErrorQueue.Count > 0)
            {
                foreach (var error in visitor.ErrorQueue)
                    _writeError(text, error.Message, error.Position.Start, error.Position.Length);

                return;
            }
        }

        private bool _generateUserMainCall(FunctionType mainFnType, out string callString)
        {
            if (mainFnType.ReturnType.Classify() == TypeClassifier.NONE)
                callString = "main(";
            else if (new SimpleType(SimpleType.SimpleClassifier.INTEGER).Equals(mainFnType.ReturnType))
                callString = "return main(";
            else
            {
                callString = "";
                return false;
            }

            if (mainFnType.Parameters.Count == 0)
                callString += ");";
            else if (mainFnType.Parameters.Count == 1)
            {
                var arg1 = mainFnType.Parameters.First().DataType;

                if (new ArrayType(new SimpleType(SimpleType.SimpleClassifier.STRING, true), -1).Equals(arg1))
                    callString += "args);";
                else
                {
                    callString = "";
                    return false;
                }
            }
            else
            {
                callString = "";
                return false;
            }

            if (!callString.StartsWith("return"))
                callString += "\nreturn 0;";

            return true;
        }
    }
}
