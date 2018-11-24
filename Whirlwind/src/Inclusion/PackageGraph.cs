using System.Collections.Generic;

using Whirlwind.Semantic;

namespace Whirlwind.Inclusion
{
    class PackageGraph
    {
        private struct PackageNode
        {
            public Package package;
            public List<string> references;

            public PackageNode(Package pkg)
            {
                package = pkg;
                references = new List<string>();
            }

            public void AddReference(string name)
            {
                if (!references.Contains(name))
                    references.Add(name);
            }
        }

        private Dictionary<string, PackageNode> _packages;

        public PackageGraph()
        {
            _packages = new Dictionary<string, PackageNode>();
        }

        public void AddPackage(Package pkg)
        {
            if (!_packages.ContainsKey(pkg.Name))
                _packages.Add(pkg.Name, new PackageNode(pkg));
        }

        public void AddPackage(string parent, Package pkg)
        {
            if (!_packages.ContainsKey(pkg.Name))
            {
                _packages.Add(pkg.Name, new PackageNode(pkg));
                _packages[parent].AddReference(pkg.Name);
            }
        }

        public bool ContainsPackage(string name)
        {
            return _packages.ContainsKey(name);
        }
    }
}
