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
        {
            if (other == this)
                return true;
            else if (other.Classify() == "POINTER")
            {
                if (Type.Classify() == "SIMPLE_TYPE" && ((SimpleType)Type).Type == SimpleType.DataType.VOID)
                    return true;
            }

            return false;
        }

        public string Classify() => "POINTER";
    }
}
