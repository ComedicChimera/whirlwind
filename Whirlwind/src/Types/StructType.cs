using System.Collections.Generic;

namespace Whirlwind.Types
{
    class StructType : DataType, IDataType
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

        private StructType(StructType str)
        {
            Name = str.Name;
            Members = str.Members;
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
            => new StructType(this);

        protected sealed override bool _coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.STRUCT_INSTANCE)
            {
                StructType st = (StructType)other;

                if (Name == st.Name && _instance && Members.Count == st.Members.Count)
                {
                    using (var e1 = Members.GetEnumerator())
                    using (var e2 = st.Members.GetEnumerator())
                    {
                        while (e1.MoveNext() && e2.MoveNext())
                        {
                            if (!e1.Current.Equals(e2.Current))
                                return false;
                        }
                    }

                    return true;
                }
            }
            return false;
        }

        public TypeClassifier Classify() => _instance ? TypeClassifier.STRUCT_INSTANCE : TypeClassifier.STRUCT;

        public bool Equals(IDataType other) => Coerce(other);
    }
}
