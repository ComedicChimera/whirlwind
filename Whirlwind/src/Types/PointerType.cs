namespace Whirlwind.Types
{
    class PointerType : DataType
    {
        public readonly DataType Type;
        public int Pointers;
        public readonly bool Owned;

        public PointerType(DataType dt, int pointers, bool owned = false)
        {
            Type = dt;
            Pointers = pointers;
            Owned = owned;
        }

        protected sealed override bool _coerce(DataType other)
        {
            if (Equals(other))
                return true;
            else if (other.Classify() == TypeClassifier.POINTER)
            {
                if (Type.Classify() == TypeClassifier.SIMPLE && ((SimpleType)Type).Type == SimpleType.SimpleClassifier.VOID)
                    return true;
            }

            return false;
        }

        public override TypeClassifier Classify() => TypeClassifier.POINTER;

        public override bool Equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.POINTER)
            {
                return Type.Equals(((PointerType)other).Type) && Pointers == ((PointerType)other).Pointers;
            }

            return false;
        }
    }
}
