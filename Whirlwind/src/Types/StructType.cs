using System.Collections.Generic;

namespace Whirlwind.Types
{
    class StructType : IDataType
    {
        private bool _instance;

        public readonly string Name;
        public readonly Dictionary<string, IDataType> Members;

        public StructType(string name)
        {
            _instance = false;
            Name = name;
            Members = new Dictionary<string, IDataType>();
        }

        public bool AddMember(string name, IDataType dt)
        {
            if (Members.ContainsKey(name))
                return false;
            Members.Add(name, dt);
            return true;
        }

        public void Instantiate()
        {
            _instance = true;
        }

        public bool Coerce(IDataType other) {
            if (other.Classify() == "STRUCT_INSTANCE")
            {
                return Members == ((StructType)other).Members && Name == ((StructType)other).Name && _instance;
            }
            return false;
        }

        public string Classify() => _instance ? "STRUCT" : "STRUCT_INSTANCE";
    }
}
