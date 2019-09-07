using System;
using System.Linq;
using System.Collections.Generic;

using Whirlwind.Types;

namespace Whirlwind.Semantic.Constexpr
{
    // perhaps consider expanding what nodes are allowed
    static class Evaluator
    {
        private static readonly string[] _validNodes =
        {
            "Array",
            "Tuple",
            "StaticGet",
            "Subscript",
            "TypeCast",
            "ChangeSign",
            "Pow",
            "Add",
            "Sub",
            "Mul",
            "Div",
            "Mod",
            "Floordiv",
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
                return ((ConstexprNode)node).ConstValue;
            else
                throw new ArgumentException("Unable to evaluate the given argument.");
        }

        private static ITypeNode _evaluateExpr(ExprNode node)
        {
            if (node.Name == "Array" || node.Name == "Tuple")
                return new ExprNode(node.Name, node.Type,
                       node.Nodes.Select(x => Evaluate(x)).ToList()
                   );
            else if (node.Name.StartsWith("Slice"))
            {
                var array = (ExprNode)node.Nodes[0];

                int begin = 0, end = array.Nodes.Count - 1, step = 1;

                var valList = node.Nodes.Skip(1).Select(x => (int)_convertToCSharpType(x.Type, ((ValueNode)Evaluate(x)).Value)).ToArray();

                switch (node.Name)
                {
                    case "Slice":
                        begin = valList[0];
                        end = valList[1];
                        break;
                    case "SliceBegin":
                        begin = valList[0];
                        break;
                    case "SliceEnd":
                        end = valList[0];
                        break;
                    case "SliceStep":
                        begin = valList[0];
                        end = valList[1];
                        step = valList[2];
                        break;
                    case "SliceBeginStep":
                        begin = valList[0];
                        step = valList[1];
                        break;
                    case "SliceEndStep":
                        end = valList[0];
                        step = valList[1];
                        break;
                    case "SlicePureStep":
                        step = valList[0];
                        break;
                }

                List<ITypeNode> nodes = array.Nodes;

                if (begin < 0)
                    begin = array.Nodes.Count + begin - 1;

                if (end < 0)
                    end = array.Nodes.Count + end - 1;

                if (end < begin)
                    return new ExprNode("Array", new ArrayType(((ArrayType)array.Type).ElementType, 0));

                if (step < 0)
                    array.Nodes.Reverse();

                return new ExprNode("Array", new ArrayType(((ArrayType)array.Type).ElementType, end - begin),
                    array.Nodes.GetRange(begin, end - begin).Where((_, i) => i % step != 0).ToList());
            }
            else if (node.Name == "InlineComparison")
            {
                var condition = _convertToCSharpType(node.Nodes[0].Type, ((ValueNode)Evaluate(node.Nodes[0])).Value);

                return Evaluate(node.Nodes[(int)condition + 1]);
            }
            else if (node.Name == "Subscript")
            {
                if (node.Nodes[0].Type.Classify() == TypeClassifier.ARRAY)
                {
                    var val = Evaluate(node.Nodes[1]);

                    if (val.Type is SimpleType && ((SimpleType)val.Type).Type == SimpleType.SimpleClassifier.INTEGER)
                    {
                        int ndx = int.Parse(((ValueNode)val).Value);

                        if (ndx < 0)
                            ndx = ((ArrayType)node.Nodes[0].Type).Size - ndx - 1;

                        if (ndx < ((ArrayType)node.Nodes[0].Type).Size)
                        {
                            return Evaluate(((ExprNode)node.Nodes[0]).Nodes[ndx]);
                        }
                        else
                            return new ValueNode("None", new NoneType());
                    }

                }

                throw new ArgumentException("Unable to evaluate a subscript on the given type");
            }
            else if (node.Name == "StaticGet")
                return ((ConstexprNode)node.Nodes[1]).ConstValue;
            else if (node.Nodes.Count == 2)
            {
                dynamic val1 = _convertToCSharpType(node.Type, ((ValueNode)Evaluate(node.Nodes[0])).Value),
                            val2 = _convertToCSharpType(node.Type, ((ValueNode)Evaluate(node.Nodes[1])).Value);

                object newVal;

                switch (node.Name)
                {
                    case "Add":
                        newVal = val1 + val2;
                        break;
                    case "Sub":
                        newVal = val1 - val2;
                        break;
                    case "Mul":
                        newVal = val1 * val2;
                        break;
                    case "Div":
                        newVal = (double)val1 / val2;
                        break;
                    case "Mod":
                        newVal = val1 % val2;
                        break;
                    case "Floordiv":
                        newVal = Math.Floor(val1 / val2);
                        break;
                    case "Pow":
                        newVal = Math.Pow(val1, val2);
                        break;
                    case "LShift":
                        newVal = val1 << val2;
                        break;
                    case "RShift":
                        newVal = val1 >> val2;
                        break;
                    case "Lt":
                        newVal = val1 < val2;
                        break;
                    case "Gt":
                        newVal = val1 > val2;
                        break;
                    case "LtEq":
                        newVal = val1 <= val2;
                        break;
                    case "GtEq":
                        newVal = val1 >= val2;
                        break;
                    case "Eq":
                        newVal = val1 == val2;
                        break;
                    case "Neq":
                        newVal = val1 != val2;
                        break;                
                    case "Or":
                        if (val1 is bool && val2 is bool)
                            newVal = val1 || val2;
                        else
                            newVal = val1 | val2;
                        break;
                    case "Xor":
                        if (val1 is bool && val2 is bool)
                            newVal = val1 != val2;
                        else
                            newVal = val1 ^ val2;
                        break;
                    // And
                    default:
                        if (val1 is bool && val2 is bool)
                            newVal = val1 && val2;
                        else
                            newVal = val1 & val2;
                        break;
                }

                return new ValueNode("Result", node.Type, newVal.ToString());
            }
            else if (node.Nodes.Count == 1)
            {
                var val = _convertToCSharpType(node.Type, ((ValueNode)Evaluate(node.Nodes[0])).Value);

                object newVal;
                switch (node.Name)
                {
                    case "ChangeSign":
                        newVal = -val;
                        break;
                    case "Complement":
                        newVal = ~val;
                        break;
                    default:
                        newVal = !val;
                        break;
                }

                return new ValueNode("Result", node.Type, newVal.ToString());
            }
            else
                throw new ArgumentException("Unable to evaluate the given argument");              
        }

        private static dynamic _convertToCSharpType(DataType dt, string value)
        {
            if (dt is SimpleType)
            {
                var sdc = ((SimpleType)dt).Type;

                switch (sdc)
                {
                    case SimpleType.SimpleClassifier.INTEGER:
                        return int.Parse(value);
                    case SimpleType.SimpleClassifier.FLOAT:
                        return float.Parse(value);
                    case SimpleType.SimpleClassifier.DOUBLE:
                        return double.Parse(value);
                    case SimpleType.SimpleClassifier.LONG:
                        return long.Parse(value);
                    case SimpleType.SimpleClassifier.BOOL:
                        return bool.Parse(value);
                    case SimpleType.SimpleClassifier.CHAR:
                        return char.Parse(value);
                    case SimpleType.SimpleClassifier.STRING:
                        return value;
                }
            }

            throw new ArgumentException("Unable to convert the given Whirlwind data type to C# data type");
        }

        public static bool TryEval(ITypeNode node)
        {
            if (node is ExprNode en && (_validNodes.Contains(node.Name) || node.Name.StartsWith("Slice")))
                return en.Nodes.All(x => TryEval(x));
            else if (node is ValueNode || node is ConstexprNode)
                return true;            

            return false;
        }
    }
}
