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
            int argOffset = 0, argCount = ft.Parameters.Count;
            var frontArgs = new List<Tuple<string, LLVMTypeRef>>();
            bool refReturn = _isReferenceType(ft.ReturnType);

            if (ft.IsBoxed)
            {
                argOffset++;
                frontArgs.Add(new Tuple<string, LLVMTypeRef>(
                    ft.IsMethod ? "this" : "$state", LLVM.PointerType(LLVM.Int8Type(), 0)
                    ));
            }          
            if (refReturn)
            {
                argOffset++;
                frontArgs.Add(new Tuple<string, LLVMTypeRef>(
                    "$rt_val", _convertType(ft.ReturnType, true)
                    ));
            }

            argCount += argOffset;           

            var arguments = new LLVMTypeRef[argCount];
            var byvalAttr = new List<int>();
            var isVarArg = false;

            Parameter param;
            for (int i = 0; i < argCount; i++)
            {
                if (i < argOffset)
                    arguments[i] = frontArgs[0].Item2;
                else
                {
                    param = ft.Parameters[i];

                    // TODO: make sure indefinite passes as list
                    if (param.Indefinite)
                        isVarArg = true;

                    arguments[i] = _convertType(param.DataType, true);
                }             
            }

            var llvmFn = LLVM.AddFunction(_module, name, LLVM.FunctionType(
                refReturn ? LLVM.VoidType() : _convertType(ft.ReturnType), 
                arguments, isVarArg));

            // handle external symbols as necessary
            LLVM.SetLinkage(llvmFn, external ? LLVMLinkage.LLVMExternalLinkage : LLVMLinkage.LLVMLinkerPrivateLinkage);

            for (int i = 0; i < argCount; i++)
            {
                LLVMValueRef llvmParam = LLVM.GetParam(llvmFn, (uint)i);

                if (i < argOffset)
                    LLVM.SetValueName(llvmParam, frontArgs[i].Item1);
                else
                    LLVM.SetValueName(llvmParam, ft.Parameters[i - argOffset].Name);
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

                // split by modulo since llvm puts type prefix before name
                // and trim off quotes in case LLVM considers name illegal and
                // artifically inserts quotes in front of it
                fnScope[param.GetValueName().Split("%").Last().Trim('\"')] = new GeneratorSymbol(param);
            }

            _scopes.Add(fnScope);            
        }
        
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
