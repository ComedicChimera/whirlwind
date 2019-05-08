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

        protected override bool _coerce(DataType other)
        {
            if (other.Classify() == TypeClassifier.REFERENCE)
                return DataType.Coerce(((ReferenceType)other).DataType);

            return false;
        }

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
                return DataType.Equals(((SelfType)other).DataType);

            return DataType.Equals(other);
        }

        protected override bool _coerce(DataType other)
        {
            if (other.Classify() == TypeClassifier.SELF)
                return DataType.Coerce(((SelfType)other).DataType);

            else if (DataType.Classify() == TypeClassifier.INTERFACE && 
                other.Classify() == TypeClassifier.INTERFACE_INSTANCE)
            {
                // allow for self types to not be problematic
                return ((InterfaceType)DataType).GetInstance().Coerce(other);
            }

            return DataType.Coerce(other);
        }

        public override TypeClassifier Classify() => TypeClassifier.SELF;

        public override DataType ConstCopy()
            => new SelfType(DataType) { Constant = true };
    }
}
