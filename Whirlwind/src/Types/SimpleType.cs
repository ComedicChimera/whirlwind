using System.Linq;

namespace Whirlwind.Types
{
    class SimpleType : DataType
    {
        public enum SimpleClassifier
        {
            BOOL, 
            BYTE,
            SHORT,
            CHAR,
            INTEGER,
            FLOAT,
            LONG, 
            DOUBLE,
            STRING
        }

        public SimpleClassifier Type { get; private set; }
        public readonly bool Unsigned;

        public SimpleType(SimpleClassifier dt, bool unsigned = false)
        {
            Type = dt;
            Unsigned = unsigned;
        }

        public override TypeClassifier Classify() => TypeClassifier.SIMPLE;

        protected sealed override bool _coerce(DataType other)
        {
            if (other is SimpleType st)
            {
                // coercing signed to unsigned is legal, but not vice versa
                // also if the signs are the same we don't care
                if (!Unsigned && st.Unsigned)
                    return false;

                if (st.Type == Type)
                    return true;

                switch (st.Type)
                {
                    // short to integer, long, double, float
                    case SimpleClassifier.SHORT:
                        return new[] { SimpleClassifier.INTEGER, SimpleClassifier.FLOAT, SimpleClassifier.LONG, SimpleClassifier.DOUBLE }
                        .Contains(Type);
                    // integer to long, double, and float
                    case SimpleClassifier.INTEGER:
                        return new[] { SimpleClassifier.FLOAT, SimpleClassifier.LONG, SimpleClassifier.DOUBLE }.Contains(Type);
                    // float to double
                    case SimpleClassifier.FLOAT:
                        return Type == SimpleClassifier.DOUBLE;
                    // char to string
                    case SimpleClassifier.CHAR:
                        return Type == SimpleClassifier.STRING;
                    // byte to everything except boolean data type
                    case SimpleClassifier.BYTE:
                        return Type != SimpleClassifier.BOOL;
                }
            }
            return false; 
        }

        protected override bool _equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.SIMPLE)
            {
                return Type.Equals(((SimpleType)other).Type) && Unsigned == ((SimpleType)other).Unsigned;
            }

            return false;
        }

        public override DataType ConstCopy()
            => new SimpleType(Type, Unsigned) { Constant = true };

        public override string ToString()
        {
            string baseString;
            int qualifierStatus = 0;

            switch (Type)
            {
                case SimpleClassifier.BOOL:
                    baseString = "bool";
                    break;
                case SimpleClassifier.BYTE:
                    baseString = "byte";
                    qualifierStatus = 2;
                    break;
                case SimpleClassifier.CHAR:
                    baseString = "char";
                    qualifierStatus = 2;
                    break;
                case SimpleClassifier.SHORT:
                    baseString = "short";
                    qualifierStatus = 1;
                    break;
                case SimpleClassifier.DOUBLE:
                    baseString = "double";
                    qualifierStatus = 1;
                    break;
                case SimpleClassifier.FLOAT:
                    baseString = "float";
                    qualifierStatus = 1;
                    break;
                case SimpleClassifier.INTEGER:
                    baseString = "int";
                    qualifierStatus = 1;
                    break;
                case SimpleClassifier.LONG:
                    baseString = "long";
                    qualifierStatus = 1;
                    break;
                default:
                    baseString = "str";
                    break;
            }

            if (qualifierStatus == 1 && Unsigned)
                return "u" + baseString;
            else if (qualifierStatus == 2 && !Unsigned)
                return "s" + baseString;
            else
                return baseString;
        }

        public override uint SizeOf()
        {
            switch (Type)
            {
                case SimpleClassifier.BOOL:
                case SimpleClassifier.BYTE:
                    return 1;
                case SimpleClassifier.SHORT:
                    return 2;
                case SimpleClassifier.INTEGER:
                case SimpleClassifier.CHAR:
                case SimpleClassifier.FLOAT:
                    return 4;
                case SimpleClassifier.LONG:
                case SimpleClassifier.DOUBLE:
                    return 8;
                default:
                    return WhirlGlobals.POINTER_SIZE + 4;
            }
        }
    }
}
