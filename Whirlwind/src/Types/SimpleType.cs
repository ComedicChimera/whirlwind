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
            TYPE,
            NULL,
            VALUE
        }

        public DataType Type { get; private set; }
        public readonly bool Unsigned;

        public SimpleType(DataType dt, bool unsigned = false)
        {
            Type = dt;
            Unsigned = unsigned;
        }

        public string Classify() => "SIMPLE_TYPE";

        public bool Coerce(IDataType other)
        {
            // null can coerce to anything
            if (Type == DataType.NULL)
                return true;
            if (other.Classify() == "SIMPLE_TYPE")
            {
                if (((SimpleType)other).Type == Type)
                    return true;
                // make sure that you are not coercing signed to unsigned
                if (!((SimpleType)other).Unsigned && Unsigned)
                    return false;
                switch (((SimpleType)other).Type)
                {
                    // integer to long and float
                    case DataType.INTEGER:
                        return new[] { DataType.FLOAT, DataType.LONG }.Contains(Type);
                    // char to integer and string
                    case DataType.CHAR:
                        return new[] { DataType.STRING, DataType.INTEGER }.Contains(Type);
                    // byte to everything except boolean and data type
                    case DataType.BYTE:
                        return !new[] { DataType.BOOL, DataType.TYPE }.Contains(Type);
                }
            }
            return false; 
        }
    }
}
