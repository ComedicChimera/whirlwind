using System;
using System.Collections.Generic;
using System.Text;

using Whirlwind.Semantic;
using Whirlwind.Types;

using LLVMSharp;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private void _generateAssignment(StatementNode stNode)
        {            
            ExprNode assignVars = (ExprNode)stNode.Nodes[0], 
                assignExprs = (ExprNode)stNode.Nodes[1];

            for (int i = 0, j = 0; i < assignVars.Nodes.Count && j < assignExprs.Nodes.Count; i++)
            {
                var assignVar = assignVars.Nodes[i];
                var assignExpr = assignExprs.Nodes[j];

                if (assignExpr is TupleType tt)
                {
                    var llvmTuple = _generateExpr(assignExpr);

                    for (int k = 0; k < tt.Types.Count; k++)
                    {
                        var elemRef = LLVM.BuildStructGEP(_builder, llvmTuple, (uint)k, "tuple_elem_ref_tmp");
                        var tupleElem = LLVM.BuildLoad(_builder, elemRef, "tuple_elem_tmp");

                        _assignTo(assignVar, tt.Types[k], tupleElem);

                        if (k < tt.Types.Count - 1)
                            assignVar = assignVars.Nodes[++i];
                    }
                }
                else
                    _assignTo(assignVar, assignExpr.Type, _generateExpr(assignExpr));

                j++;
            }
        }

        private void _assignTo(ITypeNode var, DataType exprType, LLVMValueRef expr)
        {
            if (!var.Type.Equals(exprType))
                expr = _cast(expr, exprType, var.Type);

            if (_isReferenceType(var.Type))
                expr = _copyRefType(expr, exprType);

            var varRef = _generateExpr(var, true);

            LLVM.BuildStore(_builder, expr, varRef);
        }
    }
}
