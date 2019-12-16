using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private delegate void GenericGenerator(BlockNode node, bool external);

        private void _generateGeneric(BlockNode node)
        {
            var genericType = (GenericType)((IdentifierNode)node.Nodes[0]).Type;
            GenericGenerator gg;

            switch (genericType.DataType.Classify())
            {
                case TypeClassifier.FUNCTION:
                    gg = (n, e) => _generateFunction(n, e, true);
                    break;
                default:
                    gg = (n, e) => _generateStruct(n, e, false);
                    break;
            }

            foreach (var generate in genericType.Generates)
            {
                _genericSuffix = ".variant." + string.Join("_", generate.GenericAliases
                    .Values.Select(x => x.LLVMName()));

                gg(generate.Block, false);
            }

            _genericSuffix = "";
        }

        private string _pushGenericSuffix(string newSuffix)
        {
            string oldSuffix = _genericSuffix;
            _genericSuffix = newSuffix;

            return oldSuffix;
        }
    }
}
