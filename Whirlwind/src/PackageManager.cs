using System.IO;
using System.Linq;
using System.Collections.Generic;

using Whirlwind.Semantic;

namespace Whirlwind
{
    // ADD STD AND GLOBAL IMPORT CAPABILITIES
    class PackageManager
    {
        private Compiler _compiler;

        private Dictionary<string, Package> _packageGraph;
        private string _importContext = "";

        private string _startDirectory = "";

        private static readonly string _extension = ".wrl";

        public PackageManager()
        {
            _packageGraph = new Dictionary<string, Package>();
            _compiler = new Compiler("config/tokens.json", "config/grammar.ebnf");
        }

        public bool Import(string basePath, bool tryGlobalImport, out Package pkg)
        {
            // preserve context
            var currentContext = _importContext;

            // convert to absolute path after applying context
            // this makes package lookups (to avoid recompilation)
            // work properly since abs path is used as key
            string path = Path.GetFullPath(currentContext + basePath);

            // try preemptive lookup before creating new package
            // prevents recursive and redundant inclusions
            if (_packageGraph.ContainsKey(path))
            {
                pkg = _packageGraph[path];

                return pkg != null;
            }              

            if (OpenPackage(path, out string text))
            {
                // reserve package slot (prevent recursion)
                _packageGraph[path] = null;

                var sl = new Dictionary<string, Symbol>();
                _compiler.Build(text, _convertPathToPrefix(path), ref sl);

                pkg = new Package(sl);

                _packageGraph[path] = pkg;

                _importContext = currentContext;

                return true;
            }
            else if (tryGlobalImport)
            {
                // try global import
                _importContext = WhirlGlobals.WHIRL_PATH + "lib/globals/";
                if (Import(basePath, false, out pkg))
                {
                    _importContext = currentContext;
                    return true;
                }

                // try standard import
                _importContext = WhirlGlobals.WHIRL_PATH + "lib/std/";
                if (Import(basePath, false, out pkg))
                {
                    _importContext = currentContext;
                    return true;
                }
            }

            pkg = null;
            return false;
        }

        public bool ImportRaw(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    // import raw only occurs at start of compilation therefore no context need
                    // be preserved ;)
                    if (path.Contains("/"))
                        _importContext = string.Join("/", path.Split("/").SkipLast(1)) + "/";
                    else if (path.Contains("\\"))
                        _importContext = string.Join("\\", path.Split("/").SkipLast(1)) + "/";

                    var text = File.ReadAllText(path);

                    // initialize starting directory
                    _startDirectory = Path.GetFullPath(string.Join("/", path.Split("/").SkipLast(1)) + "/");

                    string idpPath = new string(path.SkipLast(4).ToArray());

                    // reserve package slot
                    _packageGraph[idpPath] = null;

                    var sl = new Dictionary<string, Symbol>();
                    _compiler.Build(text, "", ref sl);

                    _packageGraph[idpPath] = new Package(sl);

                    return true;
                }
                catch (FileNotFoundException)
                {
                    // fail silently cause it already defaults to false
                }
            }

            return false;
        }

        public bool OpenPackage(string path, out string text)
        {
            // must be a directory since it has not had extension appended yet
            if (File.Exists(path))
            {
                path += "/__api__" + _extension;

                if (File.Exists(path))
                {
                    _setImportContext(path.Remove(path.Length - 8 - _extension.Length - 1));

                    text = File.ReadAllText(path);
                    return true;
                }
            }
            else
            {
                path += _extension;

                if (File.Exists(path))
                {
                    if (path.Contains("/"))
                        _setImportContext(path.Split('/').SkipLast(1).Aggregate((a, b) => a + "/" + b) + "/");

                    text = File.ReadAllText(path);
                    return true;
                }
            }

            text = "";
            return false;
        }

        private void _setImportContext(string newContext)
        {
            if (newContext != _importContext)
                _importContext = newContext;
        }

        private string _convertPathToPrefix(string path)
        {
            int i = 0;
            for (; i < path.Length && i < _startDirectory.Length; i++)
            {
                if (path[i] != _startDirectory[i])
                    break;
            }

            return new string(path.Skip(i).ToArray())
                .Replace("\\", "::") + "::";
        }
    }
}
