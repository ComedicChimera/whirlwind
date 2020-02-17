namespace Whirlwind.Types
{
    interface IIterable
    {
        DataType GetIterator();
    }

    class ArrayType : DataType, IIterable
    {
        public readonly DataType ElementType;
        public readonly int Size; // size = -1 for unsized array

        public ArrayType(DataType elementType, int size)
        {
            ElementType = elementType;
            Size = size;
        }

        public override TypeClassifier Classify() => TypeClassifier.ARRAY;

        protected sealed override bool _coerce(DataType other)
        {
            if (other.Classify() == TypeClassifier.ARRAY)
            {
                return ElementType.Coerce(((ArrayType)other).ElementType) && (Size < 0 || ((ArrayType)other).Size == Size);
            }
            else if (other.Classify() == TypeClassifier.LIST)
            {
                return ElementType.Coerce(((ListType)other).ElementType) && Size < 0;
            }
            return false;
        }

        public DataType GetIterator() => ElementType;

        protected override bool _equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.ARRAY)
            {
                return Size == ((ArrayType)other).Size && ElementType.Equals(((ArrayType)other).ElementType);
            }

            return false;
        }

        public override DataType ConstCopy()
            => new ArrayType(ElementType, Size) { Constant = true };

        public override string ToString()
            => $"array[{ElementType.ToString()}{(Size == -1 ? "]" : $", {Size}]")}";

        public override string LLVMName()
            => _makeLLVMSafe($"array[{ElementType.LLVMName()}{(Size == -1 ? "]" : $", {Size}]")}");
    }

    class ListType : DataType, IIterable
    {
        public readonly DataType ElementType;

        public ListType(DataType elementType)
        {
            ElementType = elementType;
        }

        public override TypeClassifier Classify() => TypeClassifier.LIST;

        protected sealed override bool _coerce(DataType other)
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

        public DataType GetIterator() => ElementType;

        protected override bool _equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.LIST)
            {
                return ElementType.Equals(((ListType)other).ElementType);
            }

            return false;
        }

        public override DataType ConstCopy()
            => new ListType(ElementType) { Constant = true };

        public override string ToString()
            => $"list[{ElementType.ToString()}]";

        public override string LLVMName()
            => _makeLLVMSafe($"list[{ElementType.LLVMName()}]");
    }

    class DictType : DataType,  IIterable
    {
        public readonly DataType KeyType, ValueType;

        public DictType(DataType keyType, DataType valueType)
        {
            KeyType = keyType;
            ValueType = valueType;
        }

        public override TypeClassifier Classify() => TypeClassifier.DICT;

        protected sealed override bool _coerce(DataType other)
        {
            if (other.Classify() == TypeClassifier.DICT)
            {
                return KeyType.Coerce(((DictType)other).KeyType) && ValueType.Coerce(((DictType)other).ValueType);
            }
            return false;
        }

        public DataType GetIterator() => KeyType;

        protected override bool _equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.DICT)
            {
                return KeyType.Equals(((DictType)other).KeyType) && ValueType.Equals(((DictType)other).ValueType);
            }

            return false;
        }

        public override DataType ConstCopy()
            => new DictType(KeyType, ValueType) { Constant = true };

        public override string ToString()
            => $"dict[{KeyType.ToString()}, {ValueType.ToString()}]";

        public override string LLVMName()
            => $"dict[{KeyType.LLVMName()}, {ValueType.LLVMName()}]";
    }
}
