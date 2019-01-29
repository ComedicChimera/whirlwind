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
            "StaticGet",
            "Subscript",
            "TypeCast",
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

        public static ITypeNode Evaluate(ITypeNode node)
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

        private static ITypeNode _evaluateExpr(ITypeNode node)
        {
            // check for slice

            switch (node.Name)
            {
                case "Array":
                case "Tuple":
                    // simplify sub nodes
                    return node;
                case "Subscript":
                    if (node.Type.Classify() == TypeClassifier.ARRAY)
                    {
                        var expr = (ExprNode)node;

                        var val = Evaluate(expr.Nodes[1]);

                        if (val.Type is SimpleType && ((SimpleType)val.Type).Type == SimpleType.DataType.INTEGER)
                        {
                            int ndx = int.Parse(((ValueNode)val).Value);

                            if (ndx < 0)
                                ndx = ((ArrayType)expr.Nodes[0].Type).Size - ndx - 1;

                            if (ndx < ((ArrayType)expr.Nodes[0].Type).Size)
                            {
                                return ((ExprNode)expr.Nodes[0]).Nodes[ndx];
                            }
                            else
                                return new ValueNode("None", new SimpleType());
                        }

                    }

                    goto default;
                default:
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
