using System.Collections.Generic;

using Whirlwind.Semantic;

namespace Whirlwind
{
    class PackageGraph
    {
        private struct PackageNode
        {
            public Package Pkg;
            public List<string> References;

            public PackageNode(Package package)
            {
                Pkg = package;
                References = new List<string>();
            }

            public void AddReference(string name)
            {
                if (!References.Contains(name))
                    References.Add(name);
            }
        }

        private Dictionary<string, PackageNode> _packages;

        public PackageGraph()
        {
            _packages = new Dictionary<string, PackageNode>();
        }

        public bool AddPackage(string path, Package pkg)
        {
            if (!_packages.ContainsKey(path))
            {
                _packages.Add(path, new PackageNode(pkg));
                return true;
            }               

            return false;
        }

        public bool AddPackage(string path, Package pkg, string parent)
        {
            if (!_packages.ContainsKey(path))
            {
                _packages.Add(path, new PackageNode(pkg));
                _packages[parent].AddReference(path);
                return true;
            }

            return false;
        }

        public bool GetPackage(string path, out Package pkg)
        {
            if (_packages.ContainsKey(path))
            {
                pkg = _packages[path].Pkg;
                return true;
            }

            pkg = null;
            return false;
        }
    }
}
