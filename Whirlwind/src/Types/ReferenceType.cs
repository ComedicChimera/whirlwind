namespace Whirlwind.Types
{
    class ReferenceType : DataType
    {
        public readonly DataType DataType;
        public readonly bool Owned;

        public ReferenceType(DataType dt, bool owned = false)
        {
            DataType = dt;
            Owned = owned;
        }

        public override bool Equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.REFERENCE)
                return DataType.Equals(((ReferenceType)other).DataType);

            return false;
        }

        protected override bool _coerce(DataType other) => Equals(other);

        public override TypeClassifier Classify() => TypeClassifier.REFERENCE;

        public override DataType ConstCopy()
            => new ReferenceType(DataType, Owned);
    }

    // self referential type
    class SelfType : DataType
    {
        public readonly DataType DataType;

        public SelfType(DataType dt)
        {
            DataType = dt;
        }

        public override bool Equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.SELF)
                return true;

            return DataType.Equals(other);
        }

        protected override bool _coerce(DataType other)
        {
            if (other.Classify() == TypeClassifier.SELF)
                return true;

            return DataType.Coerce(other);
        }

        public override TypeClassifier Classify() => TypeClassifier.SELF;

        public override DataType ConstCopy()
            => new SelfType(DataType) { Constant = true };
    }
}
