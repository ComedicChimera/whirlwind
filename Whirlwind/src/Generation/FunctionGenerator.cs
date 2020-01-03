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
        private void _generateFunction(BlockNode node, bool external, bool global, string prefix="")
        {
            var idNode = (IdentifierNode)node.Nodes[0];

            string name = idNode.IdName;

            _table.Lookup(name, out Symbol sym);

            bool exported = sym.Modifiers.Contains(Modifier.EXPORTED);
            bool externLink = external || exported;

            string llvmPrefix = exported ? _randPrefix + prefix : prefix;
            name += _genericSuffix;

            LLVMValueRef llvmFn;

            if (sym.DataType is FunctionGroup fg)
            {
                var fn = (FunctionType)idNode.Type;
                string fgSuffix = "." + string.Join(",", fn.Parameters.Select(x => x.DataType.LLVMName()));

                llvmFn = _generateFunctionPrototype(llvmPrefix + name + fgSuffix, fn, externLink);

                if (!external)
                    _appendFunctionBlock(llvmFn, node);

                LLVM.VerifyFunction(llvmFn, LLVMVerifierFailureAction.LLVMPrintMessageAction);

                if (global)
                    _addGlobalDecl(prefix + name + fgSuffix, llvmFn);
            }
            else
            {
                FunctionType fn = (FunctionType)idNode.Type;

                llvmFn = _generateFunctionPrototype(llvmPrefix + name, fn, externLink);

                if (!external)
                    _appendFunctionBlock(llvmFn, node);

                LLVM.VerifyFunction(llvmFn, LLVMVerifierFailureAction.LLVMPrintMessageAction);

                if (global)
                    _addGlobalDecl(prefix + name, llvmFn);
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

        private void _appendFunctionBlock(LLVMValueRef vref, BlockNode block)
        {
            _fnBlocks.Add(new Tuple<LLVMValueRef, BlockNode>(vref, block));
        }

        private void _buildFunctionBlock(LLVMValueRef vref, BlockNode block)
        {
            LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, vref, "entry"));

            _declareFnArgs(vref);

            if (_generateBlock(block.Block))
                LLVM.BuildRetVoid(_builder);

            _scopes.RemoveLast();
        }

        private void _buildFunctionBlock(LLVMValueRef vref, FnBodyBuilder fbb)
        {
            LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, vref, "entry"));

            _declareFnArgs(vref);

            if (fbb(vref))
                LLVM.BuildRetVoid(_builder);

            _scopes.RemoveLast();
        }

        private void _declareFnArgs(LLVMValueRef fnRef)
        {
            var fnScope = new Dictionary<string, GeneratorSymbol>();

            var paramCount = LLVM.CountParams(fnRef);
            for (uint i = 0; i < paramCount; i++)
            {
                var param = LLVM.GetParam(fnRef, i);

                fnScope[param.GetValueName()] = new GeneratorSymbol(param);
            }

            _scopes.Add(fnScope);            
        }

        /*private bool _returnsAsArgPtr(FunctionType ft)
            => new[] { TypeClassifier.STRUCT_INSTANCE, TypeClassifier.TYPE_CLASS_INSTANCE,
                       TypeClassifier.INTERFACE_INSTANCE, TypeClassifier.ARRAY, TypeClassifier.LIST,
                       TypeClassifier.DICT, TypeClassifier.TUPLE }.Contains(ft.ReturnType.Classify());*/
        
        private ArgumentList _createArgsList(ExprNode enode)
            => new ArgumentList(
                    enode.Nodes.Skip(1).Where(x => x.Name != "NamedArgument")
                        .Select(x => x.Type).ToList(),
                    enode.Nodes.Skip(1).Where(x => x.Name == "NamedArgument")
                        .ToDictionary(
                        x => ((IdentifierNode)((ExprNode)x).Nodes[0]).IdName,
                        x => x.Type)
                    );
    }
}
