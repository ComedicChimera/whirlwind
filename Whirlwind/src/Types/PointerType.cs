namespace Whirlwind.Types
{
    class PointerType : IDataType
    {
        public readonly IDataType Type;
        public int Pointers;

        public PointerType(IDataType dt, int pointers)
        {
            Type = dt;
            Pointers = pointers;
        }

        public bool Coerce(IDataType other)
            => false;

        public string Classify() => "POINTER";
    }
}
