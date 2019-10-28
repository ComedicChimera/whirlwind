using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using LLVMSharp;

using Whirlwind.Semantic;
using Whirlwind.Types;

namespace Whirlwind.Generation
{
    partial class Generator
    {
        private LLVMValueRef _generateExpr(ITypeNode expr)
        {
            if (expr is ValueNode vnode)
                return _generateExprValue(vnode);
            // hopefully this is ok
            else if (expr is IdentifierNode inode)
                return _getNamedValue(inode.IdName);
            else if (expr is ConstexprNode cnode)
                return _generateExpr(cnode.ConstValue);
            // only other option is expr node
            else
            {
                var enode = (ExprNode)expr;

                switch (expr.Name)
                {
                    case "Add":
                        
                        break;
                }
            }

            return _ignoreValueRef();
        }

        private List<LLVMValueRef> _buildOperands(List<ITypeNode> nodes, DataType exprType)
        {
            var results = new List<LLVMValueRef>();

            foreach (var node in nodes)
            {
                var g = _generateExpr(node);

                if (!exprType.Equals(node.Type))
                    results.Add(_cast(g, node.Type, exprType));
                else
                    results.Add(g);
            }

            return results;
        }

        private bool _buildOperOverload(ITypeNode expr, out LLVMValueRef res)
        {
            res = _ignoreValueRef();
            return false;
        } 
    }
}
