using System.Collections.Generic;

namespace Whirlwind.Types
{
    class TupleType : IDataType
    {
        public readonly List<IDataType> Types;

        public TupleType(List<IDataType> types)
        {
            Types = types;
        }

        public TypeClassifier Classify() => TypeClassifier.TUPLE;

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == Classify())
            {
                if (((TupleType)other).Types == Types)
                    return true;
            }
            return false;
        }
    }
}
