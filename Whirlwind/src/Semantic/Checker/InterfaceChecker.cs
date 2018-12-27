using Whirlwind.Types;

using System.Linq;

namespace Whirlwind.Semantic.Checker
{
    // check all special type interfaces
    static partial class Checker
    {
        static string[] _modifiableNodes =
        {
                "Dereference",
                "Subscript",
                "Slice",
                "SliceBegin",
                "SliceEnd",
                "SliceStep",
                "SlicePureStep",
                "SliceBeginStep",
                "SliceEndStep",
                "GetMember"
        };

        public static bool Hashable(IDataType dt)
        {
            if (new[] { TypeClassifier.ARRAY, TypeClassifier.DICT, TypeClassifier.LIST, TypeClassifier.FUNCTION, TypeClassifier.TUPLE }.Contains(dt.Classify()))
                return false;
            return true;
        }

        public static bool Iterable(IDataType dt)
        {
            if (dt is IIterable)
                return true;

            if (HasOverload(dt, "__next__", out IDataType returnType))
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

        /* Check if a given node is modifiable
         * not a direct inferface mirror and really more predicated on checking constants
         * but it somewhat fits with the rest of these
         */
        public static bool Modifiable(ITypeNode node)
        {
            if (node.Name == "Identifier")
                return !((IdentifierNode)node).Constant;

            if (_modifiableNodes.Contains(node.Name))
            {
                if (node.Name == "GetMember" && ((IdentifierNode)((ExprNode)node).Nodes[1]).Constant)
                    return false;

                var rootNode = ((ExprNode)node).Nodes[0];

                // this pointer is immutable, but its members might not be, so we don't bubble this pointer constancy
                if (rootNode.Name == "Identifier" && ((IdentifierNode)rootNode).IdName == "$THIS")
                    return true;

                return Modifiable(rootNode);
            }
                

            return true;
        }

        // also not technically an interface, but it exhibits similar behavior
        // checks if it is a number type (int, float, ect.)
        public static bool Numeric(IDataType dt)
        {
            if (dt.Classify() == TypeClassifier.SIMPLE)
            {
                return !new[] {
                        SimpleType.DataType.STRING, SimpleType.DataType.BYTE, SimpleType.DataType.BOOL, SimpleType.DataType.CHAR
                    }.Contains(((SimpleType)dt).Type);
            }
            return false;
        }

        public static bool IsException(IDataType dt)
        {
            return false;
        }
    }
}
