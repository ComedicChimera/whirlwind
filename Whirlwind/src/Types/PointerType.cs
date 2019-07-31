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
            if (other is PointerType pt)
                return DataType.Coerce(pt.DataType) && Pointers == pt.Pointers;

            return false;
        }

        public override TypeClassifier Classify() => TypeClassifier.POINTER;

        protected override bool _equals(DataType other)
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
