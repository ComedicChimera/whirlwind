using System.Linq;

using Whirlwind.Types;

namespace Whirlwind.Semantic.Checker
{
    // check all special type interfaces
    static partial class Checker
    {
        static string[] _lValueNodes =
        {
                "Subscript",
                "Slice",
                "SliceBegin",
                "SliceEnd",
                "SliceStep",
                "SlicePureStep",
                "SliceBeginStep",
                "SliceEndStep",
                "GetMember",
                "NullableGetMember",
                "StaticGet",
                "HeapAllocSize",
                "HeapAllocType",
                "HeapAllocStruct"
        };

        public static bool Hashable(DataType dt)
        {
            if (new[] { TypeClassifier.ARRAY, TypeClassifier.DICT, TypeClassifier.LIST, TypeClassifier.FUNCTION, TypeClassifier.TUPLE }.Contains(dt.Classify()))
                return false;
            return true;
        }

        public static bool Iterable(DataType dt)
        {
            if (dt is IIterable)
                return true;

            if (HasOverload(dt, "__<-__", out DataType returnType))
            {
                if (returnType.Classify() == TypeClassifier.STRUCT_INSTANCE)
                {
                    // only one element struct
                    if (((StructType)returnType).Name == "Element")
                        return true;
                }
            }

            return false;
        }

        /* Check if a given node is mutable
         * not a direct inferface mirror and really more predicated on checking constants
         * but it somewhat fits with the rest of these
         */
        public static bool Mutable(ITypeNode node)
        {
            return LValueExpr(node) && !node.Type.Constant;
        }

        // Check is a given expression is a valid l-valued expression
        public static bool LValueExpr(ITypeNode node)
        {
            if (node is IdentifierNode || node is ConstexprNode)
                return node.Type.Category == ValueCategory.LValue;
            else if (node is ExprNode enode)
            {
                if (node.Type.Category == ValueCategory.RValue)
                    return false;

                if (node.Name == "Dereference" || node.Name == "NullableDereference")
                    return true;

                if (_lValueNodes.Contains(node.Name))
                {
                    if (node.Name.EndsWith("GetMember") && enode.Nodes[0] is IdentifierNode idNode && idNode.IdName == "$THIS")
                        return LValueExpr(enode.Nodes[1]);

                    return enode.Nodes.All(x => LValueExpr(x));
                }
            }

            return false;
        }

        // also not technically an interface, but it exhibits similar behavior
        // checks if it is a number type (int, float, ect.)
        public static bool Numeric(DataType dt)
        {
            if (dt.Classify() == TypeClassifier.SIMPLE)
                return !new[] { SimpleType.SimpleClassifier.STRING, SimpleType.SimpleClassifier.CHAR, SimpleType.SimpleClassifier.BOOL }.Contains(((SimpleType)dt).Type);

            return false;
        }
    }
}
