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

            _globalStructs[name] = llvmStruct;

            var memberDict = new Dictionary<string, ITypeNode>();
            bool needsInitMembers = false;

            // process constructor
            bool hasMultipleConstructors = node.Block.Where(x => x is BlockNode).Count() > 1;
            foreach (var item in node.Block)
            {
                if (item is ExprNode eNode)
                {
                    if (memberDict.Count == 0)
                        needsInitMembers = true;

                    foreach (var elem in eNode.Nodes.Skip(1))
                        memberDict.Add(((IdentifierNode)elem).IdName, eNode.Nodes[0]);
                }
                else
                {
                    BlockNode constructor = (BlockNode)item;
                    FunctionType cft = (FunctionType)(constructor.Nodes[0].Type);
                    cft.ReturnType = st;
                    string suffix = ".constructor";

                    if (hasMultipleConstructors)
                        suffix += "." + string.Join(",", cft.Parameters.Select(x => x.DataType.LLVMName()));

                    var llvmConstructor = _generateFunctionPrototype(name + suffix, cft, exported);
                    _globalScope[name + suffix] = llvmConstructor;

                    if (constructor.Block.Count > 0)
                    {
                        LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, llvmConstructor, "entry"));

                        // build new struct here

                        // build init members call
                        if (needsInitMembers)
                        {

                        }

                        // generate block with declared arguments
                        _generateBlock(node.Block);

                        LLVM.VerifyFunction(llvmConstructor, LLVMVerifierFailureAction.LLVMPrintMessageAction);
                    }
                }
            }

            if (needsInitMembers)
            {
                var initFn = _generateFunctionPrototype(name + ".$_initMembers", new FunctionType(new List<Parameter>
                            { new Parameter("$THIS", new PointerType(st, false), false, false, false, false) },
                           new NoneType(), false), false);

                LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, initFn, "entry"));

                // build init members content

                LLVM.BuildRetVoid(_builder);

                LLVM.VerifyFunction(initFn, LLVMVerifierFailureAction.LLVMPrintMessageAction);
            }
        }

        private void _generateInterf(BlockNode node)
        {
            var idNode = (IdentifierNode)node.Nodes[0];
            var interfType = (InterfaceType)idNode.Type;

            var methods = new List<LLVMTypeRef>();

            foreach (var method in interfType.Methods)
            {
                if (method.Key.DataType is FunctionType ftType)
                    methods.Add(_convertType(ftType));
            }

            var vtableStruct = LLVM.StructCreateNamed(_ctx, idNode.Name + ".__vtable");
            vtableStruct.StructSetBody(methods.ToArray(), false);

            var interfStruct = LLVM.StructCreateNamed(_ctx, idNode.Name);
            interfStruct.StructSetBody(new[]
            {
                LLVM.Int16Type(),
                vtableStruct
            }, false);
        }

        private void _generateTypeClass(BlockNode node, bool packed)
        {

        }
    }
}
