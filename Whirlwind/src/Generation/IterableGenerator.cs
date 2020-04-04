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
        struct WhirlIterator
        {
            public LLVMValueRef IterRef;
            public Dictionary<string, Tuple<LLVMValueRef, int>> IterVars;
            public DataType IterDataType;

            public WhirlIterator(LLVMValueRef iterRef, Dictionary<string, Tuple<LLVMValueRef, int>> iterVars, DataType idt)
            {
                IterRef = iterRef;
                IterVars = iterVars;
                IterDataType = idt;
            }
        }

        private WhirlIterator _setupIterator(ExprNode iteratorNode)
        {
            var rootIterable = _generateExpr(iteratorNode.Nodes[0]);
            var iterRef = _getIterRef(rootIterable, iteratorNode.Nodes[0].Type);

            var iterVars = new Dictionary<string, Tuple<LLVMValueRef, int>>();
            for (int i = 1; i < iteratorNode.Nodes.Count; i++)
            {
                var item = iteratorNode.Nodes[i];

                string idName = ((IdentifierNode)item).IdName;
                if (idName != "_")
                {
                    iterVars[idName] = new Tuple<LLVMValueRef, int>(
                    LLVM.BuildAlloca(_builder, _convertType(item.Type, true), idName),
                    i);
                }               
            }

            return new WhirlIterator(iterRef.Item1, iterVars, iterRef.Item2);
        }

        private Tuple<LLVMValueRef, DataType> _getIterRef(LLVMValueRef iterable, DataType iterableType)
        {
            var iteratorType = _getNoArgMethodRtType(_getInterfaceOf(iterableType), "iter");

            return new Tuple<LLVMValueRef, DataType>
                    (_callMethod(iterable, iterableType, "iter", iteratorType), iteratorType);
        }

        private LLVMBasicBlockRef _buildIteratorUpdateBlock(WhirlIterator iter, LLVMBasicBlockRef bodyBlock, LLVMBasicBlockRef exitBlock)
        {
            // rt prefix refers to value actually returned by the next function
            // just next val refers to the value stored by the returned value of the next function

            var nextRtType = _getNoArgMethodRtType(_getInterfaceOf(iter.IterDataType), "next");
            var nextRtVal = _callMethod(iter.IterRef, iter.IterDataType, "next", nextRtType);

            var nextRtValValidElem = LLVM.BuildStructGEP(_builder, nextRtVal, 1, "next_rtval_valid_elem_ptr_tmp");
            var nextRtValValid = LLVM.BuildLoad(_builder, nextRtValValidElem, "next_rtval_valid_tmp");

            // check next value for validity before we extract and evaluate
            var setupBlock = LLVM.AppendBasicBlock(_currFunctionRef, "iter_body_setup");
            LLVM.BuildCondBr(_builder, nextRtValValid, setupBlock, exitBlock);

            LLVM.PositionBuilderAtEnd(_builder, setupBlock);
            var nextValElem = LLVM.BuildStructGEP(_builder, nextRtVal, 0, "next_rtval_val_elem_ptr_tmp");
            var nextVal = LLVM.BuildLoad(_builder, nextValElem, "next_val_tmp");
            var nextValType = ((TupleType)nextRtType).Types[0];

            foreach (var item in iter.IterVars)
            {
                _scopes.Last()[item.Key] = new GeneratorSymbol(item.Value.Item1, true);

                LLVMValueRef currNextVal;
                if (nextValType is TupleType)
                {
                    var currIterValElemPtr = LLVM.BuildStructGEP(_builder, nextVal, 
                        (uint)item.Value.Item2, $"next_val{item.Value.Item2}_elem_ptr_tmp");

                    currNextVal = LLVM.BuildLoad(_builder, currIterValElemPtr, $"next_val{item.Value.Item2}_tmp");
                }
                else
                    currNextVal = nextVal;

                LLVM.BuildStore(_builder, currNextVal, item.Value.Item1);
            }

            LLVM.BuildBr(_builder, bodyBlock);

            return setupBlock;
        }
    }
}
