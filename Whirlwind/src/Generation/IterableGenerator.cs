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
            public Dictionary<string, LLVMValueRef> IterVars;
            public DataType IterDataType;

            public WhirlIterator(LLVMValueRef iterRef, Dictionary<string, LLVMValueRef> iterVars, DataType idt)
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

            var iterVars = new Dictionary<string, LLVMValueRef>();
            foreach (var item in iteratorNode.Nodes.Skip(1))
            {
                string idName = ((IdentifierNode)item).IdName;
                iterVars[idName] = LLVM.BuildAlloca(_builder,
                    _convertType(item.Type, true),
                    idName);
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

            var e = iter.IterVars.GetEnumerator();
            for (uint i = 0; i < iter.IterVars.Count; i++)
            {
                e.MoveNext();
                
                var item = e.Current;

                _scopes.Last()[item.Key] = new GeneratorSymbol(item.Value, true);

                LLVMValueRef currNextVal;
                if (nextValType is TupleType)
                {
                    var currIterValElemPtr = LLVM.BuildStructGEP(_builder, nextVal, i, "next_val#_elem_ptr_tmp".Replace("#", i.ToString()));
                    currNextVal = LLVM.BuildLoad(_builder, currIterValElemPtr, "next_val#_tmp".Replace("#", i.ToString()));
                }
                else
                    currNextVal = nextVal;

                LLVM.BuildStore(_builder, currNextVal, item.Value);
            }

            LLVM.BuildBr(_builder, bodyBlock);

            return setupBlock;
        }
    }
}
