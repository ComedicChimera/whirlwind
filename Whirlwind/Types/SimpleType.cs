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
            TYPE
        }

        public DataType Type { get; private set; }

        public bool IsReference = false;

        public SimpleType(DataType dt)
        {
            Type = dt;
        }

        public string Classify() => "SIMPLE_TYPE";

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == "SIMPLE_TYPE")
            {
                if (((SimpleType)other).Type == Type)
                    return true;
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
            else if (other.Classify() == "UNION")
                return other.Coerce(this);
            return false; 
        }
    }
}
