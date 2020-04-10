using System.Linq;
using System.Collections.Generic;

using Whirlwind.Types;

namespace Whirlwind.Semantic.Checker
{
    // check all special type properties
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
                "HeapAllocStruct",
                "TypeCast"
        };

        public static bool Hashable(DataType dt)
        {
            // TODO: check for whether or not object has `__hash__` on structs, type classes, and interfaces

            if (new[] { TypeClassifier.ARRAY, TypeClassifier.DICT, TypeClassifier.LIST, TypeClassifier.FUNCTION }.Contains(dt.Classify()))
                return false;
            
            return true;
        }

        public static bool Iterable(DataType dt, InterfaceType iterableInterf)
        {
            if (dt is IIterableType)
                return true;

            return iterableInterf.Coerce(dt);
        }

        // Check if an expression is mutable
        public static bool Mutable(ITypeNode node)
        {
            return LValueExpr(node) && !node.Type.Constant;
        }

        // Check if a given expression is a valid l-valued expression
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
                    if (node.Name.EndsWith("GetMember") && enode.Nodes[0] is IdentifierNode idNode && idNode.IdName == "this")
                        return LValueExpr(enode.Nodes[1]);

                    return enode.Nodes.All(x => LValueExpr(x));
                }
            }

            return false;
        }

        // checks if it is a number type (int, float, ect.)
        public static bool Numeric(DataType dt)
        {
            if (dt.Classify() == TypeClassifier.SIMPLE)
                return !new[] { SimpleType.SimpleClassifier.STRING, SimpleType.SimpleClassifier.BOOL }.Contains(((SimpleType)dt).Type);

            return false;
        }

        // checks whether a type has defined equality comparison with itself
        public static bool Comparable(DataType dt, string equalityComparer="==", bool willDecompose=false)
        {
            if (_isComparableType(dt))
                return true;
            else if (willDecompose && !(dt is CustomOpaqueType))
                return true;
            else
            {
                bool hasCompOverload = HasOverload(dt, $"__{equalityComparer}__",
                    new ArgumentList(new List<DataType> { dt }), out DataType rtType);

                return hasCompOverload && new SimpleType(SimpleType.SimpleClassifier.BOOL).Equals(rtType);
            }
        }

        // checks if a type is innately comparable
        private static bool _isComparableType(DataType dt)
        {
            if (dt is AnyType || dt is CustomInstance || dt is InterfaceType)
                return false;

            // literally did not know I could do this, very cash money feature, good job c# devs
            switch (dt)
            {
                case ArrayType at:
                    return _isComparableType(at.ElementType);
                case ListType lt:
                    return _isComparableType(lt.ElementType);
                case DictType dct:
                    return _isComparableType(dct.KeyType) && _isComparableType(dct.ValueType);
                case StructType st:
                    return st.Members.All(x => _isComparableType(x.Value.DataType));
                case TupleType tt:
                    return tt.Types.All(x => _isComparableType(x));
                default:
                    return true;
            }
        }
    }
}
