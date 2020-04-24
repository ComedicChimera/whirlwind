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
        private void _generateFunction(BlockNode node, bool external, bool global, string prefix="")
        {
            var idNode = (IdentifierNode)node.Nodes[0];

            string name = idNode.IdName;
            var sym = _symTable[name];

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
                    _appendFunctionBlock(llvmFn, fn.ReturnType, node);

                LLVM.VerifyFunction(llvmFn, LLVMVerifierFailureAction.LLVMPrintMessageAction);

                if (global)
                    _addGlobalDecl(prefix + name + fgSuffix, llvmFn);
            }
            else
            {
                FunctionType fn = (FunctionType)idNode.Type;

                llvmFn = _generateFunctionPrototype(llvmPrefix + name, fn, externLink);

                if (!external)
                    _appendFunctionBlock(llvmFn, fn.ReturnType, node);

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

                if (ft.IsMethod)
                {
                    frontArgs.Add(new Tuple<string, LLVMTypeRef>("this", _thisPtrType));
                }
                else
                {
                    frontArgs.Add(new Tuple<string, LLVMTypeRef>("$state", _i8PtrType));
                }                
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
                    param = ft.Parameters[i - argOffset];

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
            LLVM.SetLinkage(llvmFn, external ? LLVMLinkage.LLVMExternalLinkage : LLVMLinkage.LLVMInternalLinkage);

            for (int i = 0; i < argCount; i++)
            {
                LLVMValueRef llvmParam = LLVM.GetParam(llvmFn, (uint)i);

                if (i < argOffset)
                    LLVM.SetValueName(llvmParam, frontArgs[i].Item1);
                else
                {
                    var ftParam = ft.Parameters[i - argOffset];

                    LLVM.SetValueName(llvmParam, ftParam.DataType.Constant ? "$c_" + ftParam.Name : ftParam.Name);
                }
                    
            }

            return llvmFn;
        }

        private void _appendFunctionBlock(LLVMValueRef vref, DataType rtType, BlockNode block)
        {
            _fnBlocks.Add(new Tuple<LLVMValueRef, DataType, BlockNode>(vref, rtType, block));
        }

        private void _appendFunctionBlock(LLVMValueRef vref, DataType rtType, FnBodyBuilder fbb)
        {
            _fnSpecialBlocks.Add(new Tuple<LLVMValueRef, DataType, FnBodyBuilder>(vref, rtType, fbb));
        }

        private void _buildFunctionBlock(LLVMValueRef vref, DataType rtType, BlockNode block)
        {
            LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, vref, "entry"));

            _declareFnArgs(vref);

            _currFunctionRef = vref;
            _currFunctionRtType = rtType;

            if (_generateBlock(block.Block))
                LLVM.BuildRetVoid(_builder);

            _scopes.RemoveLast();
        }

        private void _buildFunctionBlock(LLVMValueRef vref, DataType rtType, FnBodyBuilder fbb)
        {
            LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlockInContext(_ctx, vref, "entry"));

            _declareFnArgs(vref);

            _currFunctionRef = vref;
            _currFunctionRtType = rtType;

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
                string paramName = param.GetValueName().Split("%").Last().Trim('\"');
                
                // if parameters is a special point or is constant, it can be added directly
                if (new[] { "this", "$state", "$rt_val" }.Contains(paramName))
                    fnScope[paramName] = new GeneratorSymbol(param);
                else if (paramName.StartsWith("$c_"))
                    fnScope[paramName.Substring(3)] = new GeneratorSymbol(param);
                // if it is a reference type
                // TODO: handle reference types in declare fn args
                // otherwise, compiler must create a variable for it
                else
                {
                    var paramVar = LLVM.BuildAlloca(_builder, param.TypeOf(), paramName + "_"); // TODO: calculate alignment here
                    LLVM.BuildStore(_builder, param, paramVar);
                    fnScope[paramName] = new GeneratorSymbol(paramVar, true);
                }
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
