using System;
using System.Collections.Generic;
using System.Text;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private void _generateFunction(BlockNode node)
        {
            var idNode = (IdentifierNode)node.Nodes[0];

            string name = idNode.IdName;

            _table.Lookup(name, out Symbol sym);

            if (sym.DataType is FunctionGroup fg)
            {

            }
            

        }
    }
}
