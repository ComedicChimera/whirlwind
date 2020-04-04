using System.Collections.Generic;

using Whirlwind.Semantic;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

using LLVMSharp;

namespace Whirlwind.Generation
{
    // class storing an already computed llvm value
    // used in optimizing expression generation
    class LLVMVRefNode : ITypeNode
    {
        public string Name { get { return "LLVMNODE"; } }
        public DataType Type { get; set; }
        public LLVMValueRef Vref;

        public LLVMVRefNode(DataType dt, LLVMValueRef vref)
        {
            Type = dt;
            Vref = vref;
        }
    }

    partial class Generator
    {
        private void _generateAssignment(StatementNode stNode)
        {            
            ExprNode assignVars = (ExprNode)stNode.Nodes[0], 
                assignExprs = (ExprNode)stNode.Nodes[1];

            string op = "";
            if (stNode.Nodes.Count == 3)
                op = ((ValueNode)stNode.Nodes[2]).Value;

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

                        _assignTo(assignVar, tt.Types[k], tupleElem, op);

                        if (k < tt.Types.Count - 1)
                            assignVar = assignVars.Nodes[++i];
                    }
                }
                else
                    _assignTo(assignVar, assignExpr.Type, _generateExpr(assignExpr), op);

                j++;
            }
        }

        private void _assignTo(ITypeNode var, DataType exprType, LLVMValueRef expr, string op)
        {
            // do not carry out assignment if we are ignoring the value
            if (var is IdentifierNode idNode && idNode.IdName == "_")
                return;

            if (op != "")
            {
                // b/c get operator overload name just trims off __ (using trim), we can use this here
                _getOperatorOverloadName(op, out string treeName);

                // determining result type of operation (difficult to do efficiently on front-end too)
                DataType exprResultType = var.Type;
                CheckOperand(ref exprResultType, exprType, op, new Syntax.TextPosition());

                var tree = new ExprNode(treeName, exprResultType, new List<ITypeNode> { var, new LLVMVRefNode(exprType, expr) });
                expr = _generateExpr(tree);

                // update the expression type for casting purposes
                exprType = exprResultType;
            }

            if (!var.Type.Equals(exprType))
                expr = _cast(expr, exprType, var.Type);

            if (_isReferenceType(var.Type))
                _copyLLVMStructTo(_generateExpr(var), expr);
            else
                LLVM.BuildStore(_builder, expr, _generateExpr(var, true));
        }
    }
}
