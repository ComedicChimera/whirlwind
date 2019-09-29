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
        private void _generateStruct(BlockNode node, bool packed)
        {
            var name = ((IdentifierNode)node.Nodes[0]).IdName;
            var st = (StructType)node.Nodes[0].Type;

            var ctx = LLVM.GetModuleContext(_module);
            var namedStruct = LLVM.StructCreateNamed(ctx, name);
            namedStruct.StructSetBody(st.Members.Select(x => _convertType(x.Value.DataType)).ToArray(), packed);

            // bruh pls help
            
            // process constructor
        }
    }
}
