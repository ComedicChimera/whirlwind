using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class TupleType : DataType
    {
        public readonly List<DataType> Types;

        public TupleType(List<DataType> types)
        {
            Types = types;
            Constant = true;
        }

        public override TypeClassifier Classify() => TypeClassifier.TUPLE;

        protected sealed override bool _coerce(DataType other)
        {
            if (other.Classify() == TypeClassifier.TUPLE)
            {
                TupleType tt = (TupleType)other;

                if (Types.Count == tt.Types.Count)
                    return Enumerable.Range(0, Types.Count).All(i => Types[i].Coerce(tt.Types[i]));
            }
            return false;
        }

        protected override bool _equals(DataType other)
        {
            if (other is TupleType tt)
                return Types.EnumerableEquals(tt.Types);

            return false;
        }

        public override DataType ConstCopy()
            => new TupleType(Types); // implicit const

        public override string ToString()
            => $"tuple({string.Join(", ", Types.Select(x => x.ToString()))})";

        public override string LLVMName()
            => $"tuple({string.Join(", ", Types.Select(x => x.LLVMName()))})";
    }
}
