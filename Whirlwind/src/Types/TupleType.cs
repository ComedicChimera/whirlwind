using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class TupleType : DataType, IDataType
    {
        public readonly List<IDataType> Types;

        public TupleType(List<IDataType> types)
        {
            Types = types;
        }

        public TypeClassifier Classify() => TypeClassifier.TUPLE;

        protected sealed override bool _coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.TUPLE)
            {
                TupleType tt = (TupleType)other;

                if (Types.Count == tt.Types.Count)
                    return Enumerable.Range(0, Types.Count).All(i => Types[i].Equals(tt.Types[i]));
            }
            return false;
        }

        public bool Equals(IDataType other) => Coerce(other);
    }
}
