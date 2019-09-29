using System;
using System.Collections.Generic;
using System.Linq;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private void _generateStruct(BlockNode node, bool exported, bool packed)
        {
            var name = ((IdentifierNode)node.Nodes[0]).IdName;
            var st = (StructType)node.Nodes[0].Type;

            var llvmStruct = LLVM.StructCreateNamed(_ctx, name);
            llvmStruct.StructSetBody(st.Members.Select(x => _convertType(x.Value.DataType)).ToArray(), packed);

            // process constructor
        }
    }
}
