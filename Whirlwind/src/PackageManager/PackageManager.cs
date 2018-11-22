using Whirlwind.Semantic;
using Whirlwind.Semantic.Visitor;

using Whirlwind.Parser;

using System.IO;
using System.Linq;

namespace Whirlwind.PackageManager
{
    class PackageManager
    {
        private PackageGraph _pg;

        private static readonly string extension = ".wrl";

        public SymbolTable Import(string name, TextPosition position)
        {
            if (!OpenPackage(name.Replace("..", "../").Replace(".", "/") + extension, out string text))
            {
                var st = new SymbolTable();
                Program.compiler.Build(text, ref st);

                if (!_pg.AddPackage(new Package(st, name.Split('.').Last())))
                    throw new SemanticException("Unable to include package multiple times", position);

                return st;
            }
            else
                throw new SemanticException("Unable to include package", position);
        }

        public SymbolTable ImportFrom(string name, string parent, TextPosition position)
        {
            if (_pg.ContainsPackage(parent))
                throw new SemanticException("Unable to recursively import package", position);

            if (!OpenPackage(name.Replace("..", "../").Replace(".", "/"), out string text))
            {
                var st = new SymbolTable();
                Program.compiler.Build(text, ref st);

                if (!_pg.AddPackage(parent, new Package(st, name.Split('.').Last())))
                    throw new SemanticException("Unable to include package multiple times", position);

                return st;
            }
            else
                throw new SemanticException("Unable to include package", position);
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
