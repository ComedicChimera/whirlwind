using Whirlwind.Semantic;

using Whirlwind.Parser;

using System.IO;
using System.Linq;

namespace Whirlwind
{
    class PackageManager
    {
        private PackageGraph _pg;
        private Compiler _compiler;

        private static readonly string extension = ".wrl";

        public PackageManager()
        {
            _pg = new PackageGraph();
            _compiler = new Compiler("config/tokens.json", "config/grammar.ebnf");
        }

        public SymbolTable Import(string name, TextPosition position)
        {
            if (OpenPackage(name.Replace("..", "../").Replace(".", "/") + extension, out string text))
            {
                var st = new SymbolTable();
                _compiler.Build(text, ref st);

                _pg.AddPackage(new Package(st, name.Split('.').Last()));

                return st;
            }
            else
                throw new SemanticException("Unable to include package", position);
        }

        public SymbolTable ImportFrom(string name, string parent, TextPosition position)
        {
            if (_pg.ContainsPackage(parent))
                throw new SemanticException("Unable to recursively import package", position);

            if (OpenPackage(name.Replace("..", "../").Replace(".", "/"), out string text))
            {
                var st = new SymbolTable();
                _compiler.Build(text, ref st);

                _pg.AddPackage(parent, new Package(st, name.Split('.').Last()));

                return st;
            }
            else
                throw new SemanticException("Unable to include package", position);
        }

        public bool ImportRaw(string path)
        {
            if (OpenPackage(path, out string text))
            {
                var st = new SymbolTable();
                _compiler.Build(text, ref st);

                _pg.AddPackage(new Package(st, path.Split('/').Last().Split('.').First()));

                return true;
            }
            else
                return false;
        }

        public bool OpenPackage(string path, out string text)
        {
            try
            {
                text = File.ReadAllText(path);
                return true;
            }
            catch (FileNotFoundException)
            {
                text = "";
                return false;
            }
        }
    }
}
