using Whirlwind.Types;

using System;
using System.Linq;

namespace Whirlwind.Semantic.Constexpr
{
    class Evaluator
    {
        private static readonly string[] _validNodes =
        {
            "Array",
            "Tuple",
            "GetMember",
            "Subscript",
            "ChangeSign",
            "Increment",
            "Decrement",
            "PostfixIncrement",
            "PostfixDecrement",
            "Pow",
            "Add",
            "Sub",
            "Mul",
            "Div",
            "Mod",
            "LShift",
            "RShift",
            "Not",
            "Gt",
            "Lt",
            "Eq",
            "Neq",
            "GtEq",
            "LtEq",
            "Or",
            "Xor",
            "And",
            "Complement",
            "FloorDiv",
            "InlineComparison",
            "NullCoalesce"
        };

        public static ValueNode Evaluate(ITypeNode node)
        {
            if (node is ExprNode)
                return _evaluateExpr(node);
            else if (node is ValueNode)
                return (ValueNode)node;
            else if (node is ConstexprNode)
                return new ValueNode("Value", node.Type, ((ConstexprNode)node).ConstValue);
            else
                throw new ArgumentException("Unable to evaluate the given argument.");
        }

        private static ValueNode _evaluateExpr(ITypeNode node)
        {
            int ndx = Array.FindIndex(_validNodes, x => x == node.Name);

            switch (ndx)
            {
                // evaluate remaining nodes
                default:
                    // check for slice
                    throw new ArgumentException("Unable to evaluate the given argument.");
            }                
        }

        public static bool TryEval(ITypeNode node)
        {
            if (node is ExprNode)
                return _validNodes.Contains(node.Name) || node.Name.StartsWith("Slice");
            else if (node is ValueNode || node is ConstexprNode)
                return true;            

            return false;
        }
    }
}
