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

            var llvmStruct = LLVM.StructType(st.Members.Select(x => _convertType(x.Value.DataType)).ToArray(), packed);
            var ctx = llvmStruct.GetTypeContext();
            LLVM.StructCreateNamed(ctx, name);

            // no idea how fix this
            // var glob = LLVM.AddGlobal(_module, llvmStruct, name);

            // process constructor
        }
    }
}
