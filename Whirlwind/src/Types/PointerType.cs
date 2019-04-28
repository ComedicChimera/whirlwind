namespace Whirlwind.Types
{
    class PointerType : DataType
    {
        public readonly DataType DataType;
        public int Pointers;
        public readonly bool Owned;

        public PointerType(DataType dt, int pointers, bool owned = false)
        {
            DataType = dt;
            Pointers = pointers;
            Owned = owned;
        }

        protected sealed override bool _coerce(DataType other)
        {
            if (Equals(other))
                return true;
            else if (other.Classify() == TypeClassifier.POINTER)
            {
                if (DataType.Classify() == TypeClassifier.SIMPLE && ((SimpleType)DataType).Type == SimpleType.SimpleClassifier.VOID)
                    return true;
            }

            return false;
        }

        public override TypeClassifier Classify() => TypeClassifier.POINTER;

        public override bool Equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.POINTER)
            {
                return DataType.Equals(((PointerType)other).DataType) && Pointers == ((PointerType)other).Pointers;
            }

            return false;
        }

        public override DataType ConstCopy()
            => new PointerType(DataType, Pointers, Owned) { Constant = true };
    }
}
