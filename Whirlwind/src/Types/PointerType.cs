namespace Whirlwind.Types
{
    class PointerType : DataType, IDataType
    {
        public readonly IDataType Type;
        public int Pointers;

        public PointerType(IDataType dt, int pointers)
        {
            Type = dt;
            Pointers = pointers;
        }

        protected sealed override bool _coerce(IDataType other)
        {
            if (Equals(other))
                return true;
            else if (other.Classify() == TypeClassifier.POINTER)
            {
                if (Type.Classify() == TypeClassifier.SIMPLE && ((SimpleType)Type).Type == SimpleType.DataType.VOID)
                    return true;
            }

            return false;
        }

        public TypeClassifier Classify() => TypeClassifier.POINTER;

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.POINTER)
            {
                return Type.Equals(((PointerType)other).Type) && Pointers == ((PointerType)other).Pointers;
            }

            return false;
        }
    }
}
