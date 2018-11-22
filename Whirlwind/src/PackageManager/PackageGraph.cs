using System.Collections.Generic;

using Whirlwind.Semantic;

namespace Whirlwind.PackageManager
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

            public bool AddReference(string name)
            {
                if (!references.Contains(name))
                {
                    references.Add(name);
                    return true;
                }

                return false;
            }
        }

        private Dictionary<string, PackageNode> _packages;

        public bool AddPackage(Package pkg)
        {
            if (!_packages.ContainsKey(pkg.Name))
            {
                _packages.Add(pkg.Name, new PackageNode(pkg));
                return true;
            }

            return false;
        }

        public bool AddPackage(string parent, Package pkg)
        {
            if (!_packages.ContainsKey(pkg.Name))
            {
                AddPackage(pkg.Name, pkg);

                return _packages[parent].AddReference(pkg.Name);
            }

            return false;
        }

        public bool ContainsPackage(string name)
        {
            return _packages.ContainsKey(name);
        }
    }
}
