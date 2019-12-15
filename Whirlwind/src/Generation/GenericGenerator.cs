using System;
using System.Collections.Generic;
using System.Text;

using Whirlwind.Semantic;
using Whirlwind.Types;
namespace Whirlwind.Generation
{
    partial class Generator
    {
        private void _generateGeneric(BlockNode node)
        {
            var genericType = (GenericType)((IdentifierNode)node.Nodes[0]).Type;

            switch (genericType.DataType.Classify())
            {
                case TypeClassifier.FUNCTION:
                    break;
            }
        }
    }
}
