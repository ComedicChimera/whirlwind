using System.Linq;
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

        public string Classify() => $"TUPLE({string.Join(", ", Types.Select(x => x.Classify()))})";

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
