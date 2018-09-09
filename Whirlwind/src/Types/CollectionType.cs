using System.Collections.Generic;

namespace Whirlwind.Types
{
    interface IIterable
    {
        IDataType GetIterator();
    }

    class ArrayType : IDataType, IIterable
    {
        public readonly IDataType ElementType;
        public readonly int Size;

        public ArrayType(IDataType elementType, int size)
        {
            ElementType = elementType;
            Size = size;
        }

        public TypeClassifier Classify() => TypeClassifier.ARRAY;

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.ARRAY)
            {
                return ElementType.Coerce(((ArrayType)other).ElementType) && ((ArrayType)other).Size == Size;
            }
            return false;
        }

        public IDataType GetIterator() => ElementType;
    }

    class ListType : IDataType, IIterable
    {
        public readonly IDataType ElementType;

        public ListType(IDataType elementType)
        {
            ElementType = elementType;
        }

        public TypeClassifier Classify() => TypeClassifier.LIST;

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.ARRAY)
            {
                return ElementType.Coerce(((ArrayType)other).ElementType);
            }
            else if (other.Classify() == TypeClassifier.LIST)
            {
                return ElementType.Coerce(((ListType)other).ElementType);
            }
            return false;
        }

        public IDataType GetIterator() => ElementType;
    }

    class MapType : IDataType, IIterable
    {
        public readonly IDataType KeyType, ValueType;

        public MapType(IDataType keyType, IDataType valueType)
        {
            KeyType = keyType;
            ValueType = valueType;
        }

        public TypeClassifier Classify() => TypeClassifier.MAP;

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.MAP)
            {
                return KeyType == ((MapType)other).KeyType && ValueType == ((MapType)other).ValueType;
            }
            return false;
        }

        public IDataType GetIterator() => new TupleType(new List<IDataType>() { KeyType, ValueType });
    }
}
