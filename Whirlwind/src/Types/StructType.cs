using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;

namespace Whirlwind.Types
{
    class StructType : DataType
    {
        public readonly string Name;
        public readonly Dictionary<string, Symbol> Members;

        private List<FunctionType> _constructors;
        private bool _instance;

        public StructType(string name)
        {
            Name = name;
            Members = new Dictionary<string, Symbol>();

            _constructors = new List<FunctionType>();
            _instance = false;

            Constant = true;
        }

        private StructType(StructType str)
        {
            Name = str.Name;
            Members = str.Members;

            _constructors = str._constructors;
            _instance = true;
        }

        public bool AddMember(Symbol sym)
        {
            if (Members.ContainsKey(sym.Name))
                return false;

            Members.Add(sym.Name, sym);
            return true;
        }

        public StructType GetInstance()
            => new StructType(this) { Constant = false } ;

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

            fnType = new FunctionType(new List<Parameter>(), new NoneType(), false);
            return false;
        }

        protected sealed override bool _coerce(DataType other)
        {
            if (other is StructType st)
                return Name == st.Name && _instance == st._instance;
            // no need to check self type instance b/c if it is self type getting checked it would be made
            // into an instance automatically anyway
            else if (other is SelfType selfDt && selfDt.Initialized && selfDt.DataType is StructType sst)
                return Name == sst.Name;
            else if (other is GenericSelfInstanceType gsit && gsit.GenericSelf != null)
            {
                // should always work but who knows
                gsit.GenericSelf.CreateGeneric(gsit.TypeList, out DataType gt);

                if (gt is StructType gst)
                    return Name == gst.Name && _instance == gst._instance;
            }

            return false;
        }

        public override TypeClassifier Classify() 
            => _instance ? TypeClassifier.STRUCT_INSTANCE : TypeClassifier.STRUCT;

        protected override bool _equals(DataType other)
        {
            if (other is StructType st)
            {
                if (Name != st.Name)
                    return false;

                if (_instance != st._instance)
                    return false;

                return Members.DictionaryEquals(st.Members);
            }

            return false;
        }

        public override DataType ConstCopy()
            => new StructType(this)
            {
                _instance = _instance,
                Constant = true
            };

        public override string ToString() => PrefixToPackageName(Name);
    }
}
