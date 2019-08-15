using Whirlwind.Semantic;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Whirlwind
{
    // ADD STD AND GLOBAL IMPORT CAPABILITIES
    class PackageManager
    {
        private PackageGraph _pg;
        private Compiler _compiler;
        private string _importContext = "";

        private static readonly string _extension = ".wrl";

        public PackageManager()
        {
            _pg = new PackageGraph();
            _compiler = new Compiler("config/tokens.json", "config/grammar.ebnf");
        }

        public bool Import(string path, out Package pkg)
        {
            // preserve context
            var currentContext = _importContext;

            // convert to absolute path after applying context
            // this makes package lookups (to avoid recompilation)
            // work properly since abs path is used as key
            path = Path.GetFullPath(currentContext + path);

            // try preemptive lookup before creating new package
            if (_pg.GetPackage(path, out pkg))
                return true;

            if (OpenPackage(path, out string text))
            {
                var sl = new Dictionary<string, Symbol>();
                _compiler.Build(text, _importContext, ref sl);

                pkg = new Package(sl);

                _pg.AddPackage(path, pkg);

                _importContext = currentContext;

                return true;
            }
            else
            {
                pkg = null;
                return false;
            }               
        }

        public bool ImportRaw(string path)
        {
            return Import(path, out Package _);
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
    }
}
