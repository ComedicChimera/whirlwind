using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class EnumType : IDataType
    {
        public readonly string Name;

        private readonly List<string> _values;
        private readonly bool _instance;

        public EnumType(string name, List<string> values, bool instance = false)
        {
            Name = name;
            _values = values;
            _instance = instance;
        }

        public bool Equals(IDataType other)
        {
            if (other.Classify() == Classify())
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

        public EnumType GetInstance() => new EnumType(Name, _values, true);

        public bool Coerce(IDataType other) => Equals(other);

        public TypeClassifier Classify() => _instance ? TypeClassifier.ENUM_MEMBER : TypeClassifier.ENUM;

    }
}
