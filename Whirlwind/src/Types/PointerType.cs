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
            if (other.Classify() == "POINTER")
            {
                return Type.Coerce(((PointerType)other).Type) && Pointers == ((PointerType)other).Pointers;
            }
            return false;
        }

        public string Classify() => "POINTER";
    }

    class ReferenceType : IDataType
    {
        public readonly IDataType Type;

        public ReferenceType(IDataType dt)
        {
            Type = dt;
        }

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == "REFERENCE")
            {
                return Type.Coerce(((ReferenceType)other).Type);
            }
            return false;
        }

        public string Classify() => "REFERENCE";
    }
}
