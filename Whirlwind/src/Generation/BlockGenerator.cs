﻿using System.Collections.Generic;

using LLVMSharp;

using Whirlwind.Semantic;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        // bool says whether or not block has a terminator (of any form)
        private bool _generateBlock(List<ITypeNode> block)
        {
            // return statements, breaks, and continues are all considered definite terminators
            // and therefore all code after them is dead code that does not need to be compiled 
            // so we can return immediately after encountering one regardless of what comes after
            foreach (var node in block)
            {
                switch (node.Name)
                {
                    case "ExprReturn":
                        {
                            var ertNode = (StatementNode)node;

                            var rtVal = _cast(_generateExpr(ertNode.Nodes[0]), ertNode.Nodes[0].Type, _currFunctionRtType);

                            if (_isReferenceType(_currFunctionRtType))
                                _returnViaRtPtr(_getNamedValue("$rt_val").Vref, rtVal);
                            else
                                LLVM.BuildRet(_builder, rtVal);

                            return false;
                        }
                    case "Return":
                        {
                            var rtNode = (StatementNode)node;

                            if (rtNode.Nodes.Count == 0)
                                LLVM.BuildRetVoid(_builder);
                            else
                            {
                                var rtVal = _cast(_generateExpr(rtNode.Nodes[0]), rtNode.Nodes[0].Type, _currFunctionRtType);

                                if (_isReferenceType(_currFunctionRtType))
                                    _returnViaRtPtr(_getNamedValue("$rt_val").Vref, rtVal);
                                else
                                    LLVM.BuildRet(_builder, rtVal);
                            }

                            return false;
                        }
                    // yield does not technically count as a terminator unless we are at the end of its function block
                    // as it does not connote a control flow change unless in that case
                    case "Yield":
                        {
                            var yldNode = (StatementNode)node;
                            var yldVal = _cast(_generateExpr(yldNode.Nodes[0]), yldNode.Nodes[0].Type, _currFunctionRtType);

                            LLVM.BuildStore(_builder, yldVal, _yieldAccumulator);
                        }
                        break;
                    case "YieldBlock":
                        {
                            _yieldAccumulator = _alloca(_currFunctionRtType, "$yield_acc", true);

                            // build yield return
                            if (_generateBlock(((BlockNode)node).Block))
                            {
                                var yldVal = LLVM.BuildLoad(_builder, _yieldAccumulator, "$yield_acc_val_tmp");

                                if (_isReferenceType(_currFunctionRtType))
                                    _returnViaRtPtr(_getNamedValue("$rt_val").Vref, yldVal);
                                else
                                    LLVM.BuildRet(_builder, yldVal);
                            }

                            return false;
                        }
                    case "Break":
                        LLVM.BuildBr(_builder, _breakLabel);
                        _breakLabelUsed = true;
                        return false;
                    case "Continue":
                        LLVM.BuildBr(_builder, _continueLabel);
                        _continueLabelUsed = true;
                        return false;
                    case "ExprStmt":
                        _generateExpr(((StatementNode)node).Nodes[0]);
                        break;
                    case "DeclareVariable":
                        _generateVarDecl((StatementNode)node);
                        break;
                    case "DeclareConstant":
                    case "DeclareConstexpr":
                        _generateConstDecl((StatementNode)node);
                        break;
                    case "Assignment":
                        _generateAssignment((StatementNode)node);
                        break;
                    case "If":
                        _generateIfStatement((BlockNode)node); ;
                        break;
                    // if compound if has terminators on all branchs then the same logic
                    // that applies to other terminating statements applies (as outlined above)
                    case "CompoundIf":
                        if (!_generateCompoundIf((BlockNode)node))
                            return false;
                        break;
                    case "ForInfinite":
                        if (!_generateForInfinite((BlockNode)node))
                            return false;
                        break;
                    case "ForCondition":
                        if (!_generateForCond((BlockNode)node))
                            return false;
                        break;
                    case "CFor":
                        if (!_generateCFor((BlockNode)node))
                            return false;
                        break;
                    case "Select":
                        if (!_generateSelectStmt((BlockNode)node))
                            return false;
                        break;
                    case "ForIterator":
                        if (!_generateForIter((BlockNode)node))
                            return false;
                        break;
                }
            }

            return true;
        }

        private void _returnViaRtPtr(LLVMValueRef rtPtr, LLVMValueRef valPtr)
        {
            _copyLLVMStructTo(rtPtr, valPtr);

            LLVM.BuildRetVoid(_builder);
        }
    }
}
