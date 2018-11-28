using System.Collections.Generic;

namespace Whirlwind.Types
{
    class StructType : IDataType
    {
        private bool _instance;

        public readonly string Name;
        public readonly Dictionary<string, IDataType> Members;

        public StructType(string name, bool instance)
        {
            Name = name;
            Members = new Dictionary<string, IDataType>();
            _instance = instance;
        }

        private StructType(string name, Dictionary<string, IDataType> members)
        {
            Name = name;
            Members = members;
            _instance = true;
        }

        public bool AddMember(string name, IDataType dt)
        {
            if (Members.ContainsKey(name))
                return false;
            Members.Add(name, dt);
            return true;
        }

        public StructType GetInstance()
        {
            return new StructType(Name, Members);
        }

        public bool Coerce(IDataType other) {
            if (other.Classify() == TypeClassifier.STRUCT_INSTANCE)
            {
                // names and instance are the only necessary distinguishing factor
                return Name == ((StructType)other).Name && _instance;
            }
            return false;
        }

        public TypeClassifier Classify() => _instance ? TypeClassifier.STRUCT_INSTANCE : TypeClassifier.STRUCT;

        public bool Equals(IDataType other) => Coerce(other);
    }
}
