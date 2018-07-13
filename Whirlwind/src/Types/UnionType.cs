using System.Linq;
using System.Collections.Generic;

namespace Whirlwind.Types
{
    class UnionType : IDataType
    {
        public List<IDataType> ValidTypes { get; private set; }

        public UnionType(List<IDataType> validTypes)
        {
            ValidTypes = validTypes;
        }

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == "UNION" && this == (UnionType)other)
                return true;
            return ValidTypes.Any(x => x.Coerce(other));
        }

        public string Classify() => "UNION";
    }
}
