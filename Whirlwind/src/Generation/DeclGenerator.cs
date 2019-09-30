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

            name = _namePrefix + name;

            var llvmStruct = LLVM.StructCreateNamed(_ctx, name);
            llvmStruct.StructSetBody(st.Members.Select(x => _convertType(x.Value.DataType)).ToArray(), packed);

            // process constructor
            bool hasMultipleConstructors = node.Block.Count > 1;
            foreach (var item in node.Block)
            {
                BlockNode constructor = (BlockNode)item;
                FunctionType cft = (FunctionType)(constructor.Nodes[0].Type);
                cft.ReturnType = st;
                string suffix = ".constructor";

                if (hasMultipleConstructors)
                    suffix += "." + string.Join(",", cft.Parameters.Select(x => x.DataType.LLVMName()));

                var llvmConstructor = _generateFunctionPrototype(name + suffix, cft, exported);

                if (constructor.Block.Count > 0)
                {
                    LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, llvmConstructor, "entry"));

                    _generateBlock(node.Block);

                    // build return type here

                    LLVM.VerifyFunction(llvmConstructor, LLVMVerifierFailureAction.LLVMPrintMessageAction);
                }
            }
        }

        private void _generateTypeClass(BlockNode node, bool packed)
        {

        }
    }
}
