using System.Linq;

namespace Whirlwind.Types
{
    class SimpleType : DataType
    {
        public enum SimpleClassifier
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

        public SimpleClassifier Type { get; private set; }
        public readonly bool Unsigned;

        public SimpleType()
        {
            Type = SimpleClassifier.VOID;
            Unsigned = false;
        }

        public SimpleType(SimpleClassifier dt, bool unsigned = false)
        {
            Type = dt;
            Unsigned = unsigned;
        }

        public override TypeClassifier Classify() => TypeClassifier.SIMPLE;

        protected sealed override bool _coerce(DataType other)
        {
            // null can coerce to anything
            if (Type == SimpleClassifier.VOID)
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
                    case SimpleClassifier.INTEGER:
                        return new[] { SimpleClassifier.FLOAT, SimpleClassifier.LONG, SimpleClassifier.DOUBLE }.Contains(Type);
                    // float to double
                    case SimpleClassifier.FLOAT:
                        return Type == SimpleClassifier.DOUBLE;
                    // char to integer and string
                    case SimpleClassifier.CHAR:
                        return Type == SimpleClassifier.STRING;
                    // byte to everything except boolean and data type
                    case SimpleClassifier.BYTE:
                        return Type != SimpleClassifier.BOOL;
                }
            }
            return false; 
        }

        public override bool Equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.SIMPLE)
            {
                return Type.Equals(((SimpleType)other).Type) && Unsigned == ((SimpleType)other).Unsigned;
            }

            return false;
        }
    }
}
