using System.Linq;

namespace Whirlwind.Types
{
    class SimpleType : DataType
    {
        public enum SimpleClassifier
        {
            BOOL, 
            CHAR,
            BYTE,
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
            if (other.Classify() == TypeClassifier.SIMPLE)
            {
                // ok to coerce signed to unsigned and vice-versa because it almost
                // never fails and if I don't, we get tons of issues

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
    }
}
