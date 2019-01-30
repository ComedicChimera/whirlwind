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
                return _evaluateExpr((ExprNode)node);
            else if (node is ValueNode)
                return (ValueNode)node;
            else if (node is ConstexprNode)
                return new ValueNode("Value", node.Type, ((ConstexprNode)node).ConstValue);
            else
                throw new ArgumentException("Unable to evaluate the given argument.");
        }

        private static ITypeNode _evaluateExpr(ExprNode node)
        {
            // check for slice

            switch (node.Name)
            {
                case "Array":
                case "Tuple":
                    return new ExprNode(node.Name, node.Type,
                        ((ExprNode)node).Nodes.Select(x => Evaluate(x)).ToList()
                    );
                case "Subscript":
                    if (node.Type.Classify() == TypeClassifier.ARRAY)
                    {
                        var val = Evaluate(node.Nodes[1]);

                        if (val.Type is SimpleType && ((SimpleType)val.Type).Type == SimpleType.DataType.INTEGER)
                        {
                            int ndx = int.Parse(((ValueNode)val).Value);

                            if (ndx < 0)
                                ndx = ((ArrayType)node.Nodes[0].Type).Size - ndx - 1;

                            if (ndx < ((ArrayType)node.Nodes[0].Type).Size)
                            {
                                return ((ExprNode)node.Nodes[0]).Nodes[ndx];
                            }
                            else
                                return new ValueNode("None", new SimpleType());
                        }

                    }

                    goto default;
                case "TypeCast":
                    {
                        var rootNode = Evaluate(node.Nodes[0]);

                        if (rootNode is ValueNode)
                            return new ValueNode(rootNode.Name, node.Type, ((ValueNode)rootNode).Value);
                        else
                            return new ExprNode(rootNode.Name, node.Type, ((ExprNode)rootNode).Nodes);
                    }
                case "ChangeSign":
                    {
                        var val = _convertToCSharpType(node.Type, ((ValueNode)node.Nodes[0]).Value);

                        string newVal = "";
                        if (val is int)
                            newVal = (-(int)val).ToString();
                        else if (val is long)
                            newVal = (-(long)val).ToString();
                        else if (val is float)
                            newVal = (-(float)val).ToString();
                        else if (val is double)
                            newVal = (-(double)val).ToString();

                        return new ValueNode(newVal, node.Type);
                    }
                
                default:
                    throw new ArgumentException("Unable to evaluate the given argument");
            }                
        }

        private static object _convertToCSharpType(IDataType dt, string value)
        {
            if (dt is SimpleType)
            {
                var sdc = ((SimpleType)dt).Type;

                switch (sdc)
                {
                    case SimpleType.DataType.INTEGER:
                        return int.Parse(value);
                    case SimpleType.DataType.FLOAT:
                        return float.Parse(value);
                    case SimpleType.DataType.DOUBLE:
                        return double.Parse(value);
                    case SimpleType.DataType.LONG:
                        return long.Parse(value);
                    case SimpleType.DataType.BOOL:
                        return bool.Parse(value);
                    case SimpleType.DataType.STRING:
                        return value;
                }
            }

            throw new ArgumentException("Unable to convert the given Whirlwind data type to C# data type");
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
