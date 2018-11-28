using System.Linq;

namespace Whirlwind.Types
{
    class SimpleType : IDataType
    {
        public enum DataType
        {
            INTEGER,
            FLOAT,
            BOOL,
            STRING,
            CHAR,
            BYTE,
            LONG,
            DOUBLE,
            VOID
        }

        public DataType Type { get; private set; }
        public readonly bool Unsigned;

        public SimpleType()
        {
            Type = DataType.VOID;
            Unsigned = false;
        }

        public SimpleType(DataType dt, bool unsigned = false)
        {
            Type = dt;
            Unsigned = unsigned;
        }

        public TypeClassifier Classify() => TypeClassifier.SIMPLE;

        public bool Coerce(IDataType other)
        {
            // null can coerce to anything
            if (Type == DataType.VOID)
                return true;
            if (other.Classify() == TypeClassifier.SIMPLE)
            {
                // make sure that you are not coercing signed to unsigned
                if (!((SimpleType)other).Unsigned && Unsigned)
                    return false;

                if (((SimpleType)other).Type == Type)
                    return true;

                switch (((SimpleType)other).Type)
                {
                    // integer to long, double, and float
                    case DataType.INTEGER:
                        return new[] { DataType.FLOAT, DataType.LONG, DataType.DOUBLE }.Contains(Type);
                    // float to double
                    case DataType.FLOAT:
                        return Type == DataType.DOUBLE;
                    // char to integer and string
                    case DataType.CHAR:
                        return Type == DataType.STRING;
                    // byte to everything except boolean and data type
                    case DataType.BYTE:
                        return Type != DataType.BOOL;
                }
            }
            return false; 
        }

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.SIMPLE)
            {
                return Type.Equals(((SimpleType)other).Type) && Unsigned == ((SimpleType)other).Unsigned;
            }

            return false;
        }
    }
}
