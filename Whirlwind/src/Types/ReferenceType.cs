namespace Whirlwind.Types
{
    class ReferenceType : DataType, IDataType
    {
        public readonly IDataType DataType;

        public ReferenceType(IDataType dt)
        {
            DataType = dt;
        }

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.REFERENCE)
                return DataType.Equals(((ReferenceType)other).DataType);

            return false;
        }

        protected override bool _coerce(IDataType other) => Equals(other);

        public TypeClassifier Classify() => TypeClassifier.REFERENCE;
    }
}
