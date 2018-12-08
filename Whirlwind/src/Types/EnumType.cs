using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class EnumMember : IDataType
    {
        public readonly string Name;
        public readonly EnumType Parent;

        public EnumMember(EnumType parent)
        {
            Name = "";
            Parent = parent;
        }

        public EnumMember(string name, EnumType parent)
        {
            Name = name;
            Parent = parent;
        }

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.ENUM_MEMBER)
                return (Name == "" || Name == ((EnumMember)other).Name) && Parent.Equals(((EnumMember)other).Parent);

            return false;
        }

        public bool Coerce(IDataType other) => Equals(other);

        public TypeClassifier Classify() => TypeClassifier.ENUM_MEMBER;
    }

    class EnumType : IDataType
    {
        public readonly string Name;
        private readonly List<string> _values;

        public EnumType(string name, List<string> values)
        {
            Name = name;
            _values = values;
        }

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.ENUM)
            {
                EnumType eo = (EnumType)other;

                if (Name == eo.Name && _values.Count == eo._values.Count)
                    return _values.Zip(eo._values, (s, o) => s == o).All(x => x);
            }

            return false;
        }

        public bool HasMember(string name)
        {
            return _values.Contains(name);
        }

        public bool Coerce(IDataType other) => Equals(other);

        public TypeClassifier Classify() => TypeClassifier.ENUM;

    }
}
