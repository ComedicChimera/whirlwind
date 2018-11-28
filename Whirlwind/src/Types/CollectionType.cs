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

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.ARRAY)
            {
                return Size == ((ArrayType)other).Size && ElementType.Equals(((ArrayType)other).ElementType);
            }

            return false;
        }
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

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.LIST)
            {
                return ElementType.Equals(((ListType)other).ElementType);
            }

            return false;
        }
    }

    class DictType : IDataType, IIterable
    {
        public readonly IDataType KeyType, ValueType;

        public DictType(IDataType keyType, IDataType valueType)
        {
            KeyType = keyType;
            ValueType = valueType;
        }

        public TypeClassifier Classify() => TypeClassifier.DICT;

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.DICT)
            {
                return KeyType.Coerce(((DictType)other).KeyType) && ValueType.Coerce(((DictType)other).ValueType);
            }
            return false;
        }

        public IDataType GetIterator() => KeyType;

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.DICT)
            {
                return KeyType.Equals(((DictType)other).KeyType) && ValueType.Equals(((DictType)other).ValueType);
            }

            return false;
        }
    }
}
