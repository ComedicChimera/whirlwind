using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;

namespace Whirlwind.Types
{
    class StructType : DataType
    {
        public readonly string Name;
        public readonly Dictionary<string, DataType> Members;

        private List<FunctionType> _constructors;
        private bool _instance;

        public StructType(string name, bool instance)
        {
            Name = name;
            Members = new Dictionary<string, DataType>();

            _constructors = new List<FunctionType>();
            _instance = instance;
        }

        private StructType(StructType str)
        {
            Name = str.Name;
            Members = str.Members;

            _constructors = str._constructors;
            _instance = true;
        }

        public bool AddMember(string name, DataType dt)
        {
            if (Members.ContainsKey(name))
                return false;
            Members.Add(name, dt);
            return true;
        }

        public StructType GetInstance()
            => new StructType(this);

        public bool AddConstructor(FunctionType fnType)
        {
            if (_constructors.Where(x => x.Coerce(fnType)).Count() == 0)
            {
                _constructors.Add(fnType);
                return true;
            }

            return false;
        }

        public bool GetConstructor(ArgumentList args, out FunctionType fnType)
        {
            var matches = _constructors.Where(x => x.MatchArguments(args));

            if (matches.Count() > 0)
            {
                fnType = matches.First();
                return true;
            }

            fnType = new FunctionType(new List<Parameter>(), new SimpleType(), false);
            return false;
        }

        protected sealed override bool _coerce(DataType other)
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

        public override TypeClassifier Classify() 
            => _instance ? TypeClassifier.STRUCT_INSTANCE : TypeClassifier.STRUCT;

        public override bool Equals(DataType other) => Coerce(other);
    }
}
