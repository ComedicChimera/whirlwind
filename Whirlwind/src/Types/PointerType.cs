namespace Whirlwind.Types
{
    class PointerType : IDataType
    {
        public IDataType Type { get; private set; }
        public int Pointers;

        public PointerType(IDataType dt, int pointers)
        {
            Type = dt;
            Pointers = pointers;
        }

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == "POINTER")
            {
                return Type.Coerce(((PointerType)other).Type) && Pointers == ((PointerType)other).Pointers;
            }
            return false;
        }

        public string Classify() => "POINTER";
    }
}
