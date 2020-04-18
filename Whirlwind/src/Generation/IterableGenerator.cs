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
                    _alloca(item.Type, idName, true),
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
            var nextValType = ((TupleType)nextRtType).Types[0];
            var nextVal = _getLLVMStructMember(nextRtVal, 0, nextValType, "next_val");          

            foreach (var item in iter.IterVars)
            {
                _scopes.Last()[item.Key] = new GeneratorSymbol(item.Value.Item1, true);

                LLVMValueRef currNextVal;
                if (nextValType is TupleType tt)
                    currNextVal = _getLLVMStructMember(nextVal, 0, tt.Types[item.Value.Item2], $"next_val.{item.Key}");
                else
                    currNextVal = nextVal;

                LLVM.BuildStore(_builder, currNextVal, item.Value.Item1);
            }

            LLVM.BuildBr(_builder, bodyBlock);

            return setupBlock;
        }
    }
}
