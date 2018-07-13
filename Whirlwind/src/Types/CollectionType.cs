using System.Linq;

namespace Whirlwind.Types
{
    class ArrayType : IDataType
    {
        public readonly IDataType ElementType;
        public readonly int Size;

        public ArrayType(IDataType elementType, int size)
        {
            ElementType = elementType;
            Size = size;
        }

        public string Classify() => "ARRAY";

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == "ARRAY")
            {
                return ElementType.Coerce(((ArrayType)other).ElementType) && ((ArrayType)other).Size == Size;
            }
            return false;
        }
    }

    class ListType : IDataType
    {
        public readonly IDataType ElementType;

        public ListType(IDataType elementType)
        {
            ElementType = elementType;
        }

        public string Classify() => "LIST";

        public bool Coerce(IDataType other)
        {
            if (new[]{ "LIST", "ARRAY" }.Contains(other.Classify()))
            {
                return ElementType.Coerce((other as ListType).ElementType);
            }
            return false;
        }
    }

    class MapType : IDataType
    {
        public readonly IDataType KeyType, ValueType;

        public MapType(IDataType keyType, IDataType valueType)
        {
            KeyType = keyType;
            ValueType = valueType;
        }

        public string Classify() => "MAP";

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == "MAP")
            {
                return KeyType == ((MapType)other).KeyType && ValueType == ((MapType)other).ValueType;
            }
            return false;
        }
    }
}
