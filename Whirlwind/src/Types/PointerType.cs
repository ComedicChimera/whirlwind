namespace Whirlwind.Types
{
    class PointerType : DataType
    {
        public readonly DataType DataType;
        public bool IsDynamicPointer;

        public PointerType(DataType dt, bool heapPtr)
        {
            DataType = dt;
            IsDynamicPointer = heapPtr;
        }

        protected sealed override bool _coerce(DataType other)
        {
            if (other is PointerType pt)
                return DataType.Coerce(pt.DataType) && IsDynamicPointer == pt.IsDynamicPointer;

            return false;
        }

        public override TypeClassifier Classify() => TypeClassifier.POINTER;

        protected override bool _equals(DataType other)
        {
            if (other is PointerType pt)
                return DataType.Equals(pt.DataType) && IsDynamicPointer == pt.IsDynamicPointer;

            return false;
        }

        public override DataType ConstCopy()
            => new PointerType(DataType, IsDynamicPointer) { Constant = true };

        public override string ToString()
            => (IsDynamicPointer ? "dyn* " : "*") + DataType.ToString();
    }
}
