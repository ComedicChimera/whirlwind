using System.IO;
using System.Linq;
using System.Collections.Generic;

using Whirlwind.Semantic;
using Whirlwind.Syntax;

namespace Whirlwind
{
    class Package
    {
        public readonly string Name;

        public Dictionary<string, ASTNode> Files;
        public PackageType Type;
        public bool Compiled;

        public Package(string name)
        {
            Name = name;
            Files = new Dictionary<string, ASTNode>();

            Compiled = false;
        }
    }

    class PackageManager
    {
        private Dictionary<string, Package> _packageGraph;
        private string _importContext = "";

        private string _startDirectory = "";

        private static readonly string _extension = ".wrl";

        public string ImportContext
        {
            get { return _importContext; }
            set
            {
                if (value != _importContext)
                    _importContext = value;
            }
        }

        public PackageManager()
        {
            _packageGraph = new Dictionary<string, Package>();
        }

        public bool LoadPackage(string path, string packageName, out Package pkg)
        {
            string fullPath = Path.GetFullPath(_importContext + path);

            if (_packageGraph.ContainsKey(fullPath))
            {
                pkg = _packageGraph[fullPath];
                return pkg.Compiled;
            }

            if (_openPackage(fullPath, out string[] files))
            {
                if (_startDirectory == "")
                    _startDirectory = fullPath;

                _importContext = fullPath;

                pkg = new Package(packageName);

                foreach (var file in files)
                {
                    if (file.EndsWith(_extension))
                        pkg.Files.Add(file, null);
                }

                return true;
            }
            else if (LoadPackage(WhirlGlobals.WHIRL_PATH + "lib/global/" + path, packageName, out pkg))
                return true;
            else if (LoadPackage(WhirlGlobals.WHIRL_PATH + "lib/std/" + path, packageName, out pkg))
                return true;

            pkg = null;
            return false;
        }

        public string ConvertPathToPrefix(string path)
        {
            path = Path.GetFullPath(path);

            int i = 0;
            for (; i < path.Length && i < _startDirectory.Length; i++)
            {
                if (path[i] != _startDirectory[i])
                    break;
            }

            return new string(path.Skip(i).ToArray())
                .Replace("\\", "::") + "::";
        }

        private bool _openPackage(string path, out string[] files)
        {
            if (Directory.Exists(path))
            {
                files = Directory.GetFiles(path);
                return true;
            }

            files = null;
            return false;
        }      
    }
}
