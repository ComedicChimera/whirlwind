using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;
using Whirlwind.Types;

using LLVMSharp;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private void _generateFunction(BlockNode node, bool external)
        {
            var idNode = (IdentifierNode)node.Nodes[0];

            string name = idNode.IdName;

            _table.Lookup(name, out Symbol sym);
            bool externLink = external || sym.Modifiers.Contains(Modifier.EXPORTED);

            // add in the package prefix
            name = _namePrefix + name;

            LLVMValueRef llvmFn;

            if (sym.DataType is FunctionGroup fg)
            {
                var fn = (FunctionType)idNode.Type;
                llvmFn = _generateFunctionPrototype(name + "." + string.Join(",", fn.Parameters.Select(x => x.DataType.LLVMName())), fn, externLink);

                if (!external)
                {
                    LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, llvmFn, "entry"));

                    // include arguments!
                    _generateBlock(node.Block);

                    if (fn.ReturnType.Classify() == TypeClassifier.NONE)
                        LLVM.BuildRetVoid(_builder);
                }

                LLVM.VerifyFunction(llvmFn, LLVMVerifierFailureAction.LLVMPrintMessageAction);
            }
            else
            {
                FunctionType fn = (FunctionType)sym.DataType;

                llvmFn = _generateFunctionPrototype(name, fn, externLink);

                LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, llvmFn, "entry"));

                // include arguments!
                _generateBlock(node.Block);

                if (fn.ReturnType.Classify() == TypeClassifier.NONE)
                    LLVM.BuildRetVoid(_builder);

                LLVM.VerifyFunction(llvmFn, LLVMVerifierFailureAction.LLVMPrintMessageAction);
            }           
        }

        private LLVMValueRef _generateFunctionPrototype(string name, FunctionType ft, bool external)
        {
            var arguments = new LLVMTypeRef[ft.Parameters.Count];
            var byvalAttr = new List<int>();
            var isVarArg = false;

            Parameter param;
            for (int i = 0; i < ft.Parameters.Count; i++)
            {
                param = ft.Parameters[i];

                if (param.Indefinite)
                    isVarArg = true;

                arguments[i] = _convertType(param.DataType);
            }


            var llvmFn = LLVM.AddFunction(_module, name, LLVM.FunctionType(_convertType(ft.ReturnType), arguments, isVarArg));

            // handle external symbols as necessary
            LLVM.SetLinkage(llvmFn, external ? LLVMLinkage.LLVMExternalLinkage : LLVMLinkage.LLVMLinkerPrivateLinkage);

            for (int i = 0; i < ft.Parameters.Count; i++)
            {
                LLVMValueRef llvmParam = LLVM.GetParam(llvmFn, (uint)i);
                LLVM.SetValueName(llvmParam, ft.Parameters[i].Name);
            }

            return llvmFn;
        }
    }
}
