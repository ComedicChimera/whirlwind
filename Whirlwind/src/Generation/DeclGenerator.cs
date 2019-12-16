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
        private delegate bool InternalBuilderAlgo(LLVMValueRef vref, BlockNode block);

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
                        // add custom constructor build algo
                    }
                }
            }

            if (needsInitMembers)
            {
                // add custom init members build algo and append
                var initFn = _generateFunctionPrototype(name + ".__initMembers", new FunctionType(new List<Parameter>
                            { new Parameter("this", new PointerType(st, false), false, false, false, false) },
                           new NoneType(), false), false);

                LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, initFn, "entry"));

                // build init members content

                LLVM.BuildRetVoid(_builder);

                LLVM.VerifyFunction(initFn, LLVMVerifierFailureAction.LLVMPrintMessageAction);
            }
        }

        private void _generateInterf(BlockNode node, string suffix="")
        {
            var idNode = (IdentifierNode)node.Nodes[0];
            var interfType = (InterfaceType)idNode.Type;

            string name = idNode.IdName + suffix;

            _table.Lookup(idNode.IdName, out Symbol interfSymbol);
            bool exported = interfSymbol.Modifiers.Contains(Modifier.EXPORTED);
            string llvmPrefix = exported ? _randPrefix : "";

            var methods = new List<LLVMTypeRef>();

            int methodNdx = 0;
            foreach (var method in interfType.Methods)
            {
                // build method if necessary
                if (method.Key.DataType is FunctionType fnType)
                {
                    // check for operator overloads

                    methods.Add(_convertType(fnType));

                    if (method.Value)
                    {
                        var llvmMethod = _generateFunctionPrototype(name + "." + method.Key.Name, fnType, exported);

                        _appendFunctionBlock(llvmMethod, ((BlockNode)node.Block[methodNdx]));
                    }
                }   
            }

            var vtableStruct = LLVM.StructCreateNamed(_ctx, llvmPrefix + name + ".__vtable");
            vtableStruct.StructSetBody(methods.ToArray(), false);

            _globalStructs[name + ".__vtable"] = vtableStruct;

            var interfStruct = LLVM.StructCreateNamed(_ctx, llvmPrefix + name);
            interfStruct.StructSetBody(new[]
            {
                LLVM.PointerType(LLVM.Int8Type(), 0),
                LLVM.Int16Type(),
                vtableStruct
            }, false);

            _globalStructs[name] = interfStruct;
        }

        private void _generateTypeClass(BlockNode node)
        {

        }

        private FnBodyBuilder _wrapBuilderFunc(BlockNode block, InternalBuilderAlgo ibo)
        {
            return delegate (LLVMValueRef vref)
            {
                return ibo(vref, block);
            };
        }
    }
}
