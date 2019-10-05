using System;
using System.Collections.Generic;
using System.Text;

using Whirlwind.Syntax;

namespace Whirlwind
{
    class PackageAssembler
    {
        private Package _package;

        public PackageAssembler(Package pkg)
        {
            _package = pkg;
        }

        public ASTNode Assemble()
        {
            return new ASTNode("");
        }
    }
}
