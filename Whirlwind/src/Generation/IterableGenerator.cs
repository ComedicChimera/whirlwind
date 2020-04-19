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
        class IterVar
        {
            public readonly bool IsTupleIterator;
            public readonly List<IterVar> TupleIterVars;

            public readonly string Name;
            public readonly LLVMValueRef Vref;

            public IterVar(string name, LLVMValueRef vref)
            {
                Name = name;
                Vref = vref;
                IsTupleIterator = false;
            }

            public IterVar(List<IterVar> tupleIterVars)
            {
                TupleIterVars = tupleIterVars;
                IsTupleIterator = true;
            }
        }

        struct WhirlIterator
        {
            public LLVMValueRef IterRef;
            public List<IterVar> IterVars;
            public DataType IterDataType;

            public WhirlIterator(LLVMValueRef iterRef, List<IterVar> iterVars, DataType idt)
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

            var iterVars = _getIterVars(iteratorNode.Nodes.Skip(1).ToList());

            return new WhirlIterator(iterRef.Item1, iterVars, iterRef.Item2);
        }

        private List<IterVar> _getIterVars(List<ITypeNode> iterNodes)
        {
            var iterVars = new List<IterVar>();
            foreach (var item in iterNodes)
            {

                if (item is IdentifierNode idNode)
                {
                    string idName = idNode.IdName;
                    if (idName != "_")
                        iterVars.Add(new IterVar(idName, _alloca(item.Type, idName)));
                }
                else
                    iterVars.Add(new IterVar(_getIterVars(((ExprNode)item).Nodes)));
            }

            return iterVars;
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

            _updateIterVars(nextVal, nextValType, iter.IterVars);

            LLVM.BuildBr(_builder, bodyBlock);

            return setupBlock;
        }

        private void _updateIterVars(LLVMValueRef nextVal, DataType nextValType, List<IterVar> iterVars)
        {
            void updateNext(LLVMValueRef dest, LLVMValueRef src, bool refAsn)
            {
                if (refAsn)
                    _copyLLVMStructTo(dest, src);
                else
                    LLVM.BuildStore(_builder, src, dest);
            }

            if (iterVars.Count == 1)
            {
                var iv = iterVars[0];

                // single tuple iterators don't happen
                var refNextType = _isReferenceType(nextValType);
                _scopes.Last().Add(iv.Name, new GeneratorSymbol(iv.Vref, !refNextType));
                updateNext(nextVal, iv.Vref, refNextType);
            }
            else
            {
                for (int i = 0; i < iterVars.Count; i++)
                {
                    var iv = iterVars[i];                  

                    var nextSubType = ((TupleType)nextValType).Types[i];
                    var nextSubVal = _getLLVMStructMember(nextVal, i, nextSubType, $"next_tuple.{i}");

                    if (iv.IsTupleIterator)
                        _updateIterVars(nextSubVal, nextSubType, iv.TupleIterVars);                        
                    else
                    {
                        var refSubType = _isReferenceType(nextSubType);
                        _scopes.Last().Add(iv.Name, new GeneratorSymbol(iv.Vref, !refSubType));
                        updateNext(iv.Vref, nextSubVal, refSubType);
                    }
                }
            }            
        }
    }
}
