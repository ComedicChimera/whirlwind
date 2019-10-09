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
        private PackageManager _pm;

        private bool _compiledMainPackage;
        private Dictionary<string, DataType> _typeImpls;

        private Package _typeImplPkg;

        public Compiler(string tokenPath, string grammarPath)
        {
            _scanner = new Scanner(WHIRL_PATH + tokenPath);

            var gramloader = new GramLoader();
            _parser = new Parser(gramloader.Load(WHIRL_PATH + grammarPath));

            _pm = new PackageManager();

            _compiledMainPackage = false;
            _typeImpls = new Dictionary<string, DataType>();

            ErrorDisplay.InitLoadedFiles();

            _typeImplPkg = _buildRaw("__core__/__buildutil__/type_impls.wrl", "type_impls");
        }

        public bool Build(string path)
        {
            return Build(path, out PackageType _);
        }

        public bool Build(string path, out PackageType pkgType)
        {
            string namePrefix = _pm.ConvertPathToPrefix(path);

            if (namePrefix == "")
                namePrefix = "";
            else if (!namePrefix.EndsWith("::"))
                namePrefix += "::";

            var currentCtx = _pm.ImportContext;

            pkgType = null;

            if (_pm.LoadPackage(path, namePrefix.Trim(':'), out Package pkg))
            {
                if (pkg.Compiled)
                {
                    pkgType = pkg.Type;
                    return true;
                }               
                    
                for (int i = 0; i < pkg.Files.Count; i++)
                {
                    string fName = pkg.Files.Keys.ElementAt(i);

                    string text = File.ReadAllText(fName);

                    if (i == 0 && !namePrefix.StartsWith("lib::std::__core__"))
                        text = "include { ... } from __core__;" + text;

                    var tokens = _scanner.Scan(text);

                    ASTNode ast = _runParser(tokens, text, fName);

                    if (ast == null)
                        return false;

                    pkg.Files[fName] = ast;
                }

                bool isMainPackage = false;
                if (!_compiledMainPackage)
                {
                    isMainPackage = true;
                    _compiledMainPackage = true;
                }

                var pa = new PackageAssembler(pkg);
                var finalAst = pa.Assemble();

                var visitor = new Visitor(namePrefix, false, _typeImpls);

                if (!_runVisitor(visitor, finalAst, pkg))
                {
                    ErrorDisplay.ClearLoadedFiles();
                    return false;
                }

                var table = visitor.Table();
                var sat = visitor.Result();

                if (isMainPackage)
                    _buildMainFile(pkg, table, sat, namePrefix);

                // clear out AST once it is no longer being used
                foreach (var key in pkg.Files.Keys)
                    pkg.Files[key] = null;

                var generator = new Generator(table, visitor.Flags(), _typeImpls, namePrefix);

                if (_runGenerator(generator, sat, pkg.Name + ".llvm"))
                {
                    pkgType = null;
                    return false;
                }

                var eTable = table.Filter(s => s.Modifiers.Contains(Modifier.EXPORTED));

                // update _pm package data
                pkg.Compiled = true;
                pkg.Type = new PackageType(eTable);

                _pm.ImportContext = currentCtx;

                pkgType = pkg.Type;
                return true;
            }
            else
                return false;
        }

        private Package _buildRaw(string corePath, string name)
        {
            corePath = WHIRL_PATH + corePath;

            var pkg = new Package(name);            

            // if exception happens here, we got problems
            string text = File.ReadAllText(corePath);

            var tokens = _scanner.Scan(text);

            var ast = _runParser(tokens, text, name);
            if (ast == null)
                return null;

            pkg.Files.Add(corePath, ast);

            var visitor = new Visitor("", false, _typeImpls);

            if (!_runVisitor(visitor, ast, pkg))
                return null;

            pkg.Files[pkg.Files.Keys.First()] = null;

            var table = visitor.Table();
            var generator = new Generator(table, visitor.Flags(), _typeImpls, "");

            if (_runGenerator(generator, visitor.Result(), "type_impls.llvm"))
            {
                pkg.Compiled = true;

                var eTable = table.Filter(x => x.Modifiers.Contains(Modifier.EXPORTED));
                pkg.Type = new PackageType(eTable);

                return pkg;
            }

            return null;
        }

        private bool _buildMainFile(Package mainPkg, SymbolTable fullTable, ITypeNode sat, string namePrefix)
        {
            if (!fullTable.Lookup("main", out Symbol symbol))
            {
                Console.WriteLine("Main package missing main function definition");
                return false;
            }

            if (symbol.DataType.Classify() != TypeClassifier.FUNCTION)
            {
                Console.WriteLine("Symbol `main` in main package must be a function");
                return false;
            }

            var userMainDefinition = (FunctionType)symbol.DataType;

            if (!_generateUserMainCall(userMainDefinition, out string userMainCall))
            {
                Console.WriteLine("Invalid main function declaration");
                return false;
            }

            string mainTempPath = WHIRL_PATH + "lib/std/__core__/__buildutils__/main.wrl";
            var mainTemplate = File.ReadAllText(mainTempPath.Replace("// $INSERT_MAIN_CALL$", userMainCall));

            var mtTokens = _scanner.Scan(mainTemplate);

            var mtAst = _runParser(mtTokens, mainTemplate, namePrefix);

            if (mtAst == null)
                return false;

            mtAst.Content.Insert(0, new ASTNode("$FILE_FLAG$" + mainPkg.Files.Count));
            mainPkg.Files.Add(mainTempPath, mtAst);

            var mtVisitor = new Visitor("", false, _typeImpls);
            mtVisitor.Table().AddSymbol(symbol.Copy());

            if (!_runVisitor(mtVisitor, mtAst, mainPkg))
                return false;

            foreach (var item in mtVisitor.Table().GetScope().Skip(1))
            {
                if (!fullTable.AddSymbol(item))
                {
                    if (item.Name == "__main")
                    {
                        Console.WriteLine("Use of reserved name in main file");
                        return false;
                    }
                }
            }

            ((BlockNode)sat).Block.AddRange(((BlockNode)mtVisitor.Result()).Block);
            return true;
        }

        private ASTNode _runParser(List<Token> tokens, string text, string package)
        {
            try
            {
                return _parser.Parse(tokens);
            }
            catch (InvalidSyntaxException isx)
            {
                ErrorDisplay.DisplayError(text, package, isx);
                return null;
            }
        }

        private bool _runVisitor(Visitor visitor, ASTNode ast, Package pkg)
        {
            try
            {
                visitor.Visit(ast);
            }
            catch (SemanticException smex)
            {
                ErrorDisplay.DisplayError(pkg, smex);
                return false;
            }

            if (visitor.ErrorQueue.Count > 0)
            {
                foreach (var error in visitor.ErrorQueue)
                    ErrorDisplay.DisplayError(pkg, error);

                return false;
            }

            // Console.WriteLine(visitor.Result().ToString());

            return true;
        }

        private bool _runGenerator(Generator generator, ITypeNode sat, string outputFile)
        {
            try
            {
                generator.Generate(sat, outputFile);
                return true;
            }
            catch (GeneratorException ge)
            {
                ErrorDisplay.DisplayError(ge, outputFile);
                return false;
            }
        }

        private bool _generateUserMainCall(FunctionType mainFnType, out string callString)
        {
            if (mainFnType.ReturnType.Classify() == TypeClassifier.NONE)
                callString = "main(";
            else if (new SimpleType(SimpleType.SimpleClassifier.INTEGER).Equals(mainFnType.ReturnType))
                callString = "exitCode = main(";
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

            return true;
        }
    }
}
