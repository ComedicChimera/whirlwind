using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;

namespace Whirlwind.Types
{
    class InterfaceType : DataType
    {
        public readonly string Name;
        public readonly List<InterfaceType> Implements;
        public bool SuperForm { get; private set; } = false;
        public Dictionary<Symbol, bool> Methods { get; private set; }

        private bool _initialized = false;

        public InterfaceType(string name)
        {
            Name = name;

            Implements = new List<InterfaceType>();

            Methods = new Dictionary<Symbol, bool>();

            Constant = true;
        }

        private InterfaceType(InterfaceType interf, bool superForm)
        {
            Name = interf.Name;

            Implements = interf.Implements;

            if (superForm)
            {
                SuperForm = true;
                Methods = new Dictionary<Symbol, bool>();

                foreach (var method in interf.Methods)
                {
                    if (method.Value)
                        Methods.Add(method.Key, method.Value);
                }
            }
            else
                Methods = interf.Methods;

            _initialized = true;
        }

        public bool AddMethod(Symbol fn, bool hasBody)
        {
            if (!Methods.Any(x => x.Key.Name == fn.Name))
            {
                Methods.Add(fn, hasBody);

                return true;
            }
            else if (fn.DataType is FunctionType fta)
            {
                Symbol symbol = Methods.Where(x => x.Key.Name == fn.Name).First().Key;

                if (symbol.DataType is FunctionType ftb && FunctionGroup.CanDistinguish(fta, ftb))
                {
                    symbol.DataType = new FunctionGroup(symbol.Name, new List<FunctionType> { fta, ftb });
                    return true;
                }
                else if (symbol.DataType is FunctionGroup)
                    return ((FunctionGroup)symbol.DataType).AddFunction((FunctionType)fn.DataType);
            }

            return false;
        }

        public bool GetFunction(string fnName, out Symbol symbol)
        {
            if (Methods.Select(x => x.Key.Name).Contains(fnName))
            {
                symbol = Methods.Keys.Where(x => x.Name == fnName).ToArray()[0];
                return true;
            }

            symbol = null;
            return false;
        }

        public override TypeClassifier Classify() => _initialized ? TypeClassifier.INTERFACE_INSTANCE : TypeClassifier.INTERFACE;

        protected sealed override bool _coerce(DataType other)
        {
            if (other.Classify() == TypeClassifier.INTERFACE)
                return Equals(other);
            else if (other.Classify() == TypeClassifier.INTERFACE_INSTANCE)
                return MatchInterface((InterfaceType)other);
            else if (other is SelfType selfDt && selfDt.Initialized && selfDt.DataType is InterfaceType sit)
                return MatchInterface(sit);
            else if (other is GenericSelfInstanceType gsit)
            {
                // interface coerce NEEDS to fail if generic self instance does not exist
                // because that means this type is being used as an incomplete type (somehow)
                if (gsit.GenericSelf == null)
                    return false;

                // should always work but who knows
                gsit.GenericSelf.CreateGeneric(gsit.TypeList, out DataType gt);

                if (gt is InterfaceType it)
                    return MatchInterface(it);
            }

            var interf = other.GetInterface();

            return MatchInterface(interf);
        }

        private bool MatchInterface(InterfaceType it)
        {
            if (Methods.Count != it.Methods.Count)
                return false;

            return Methods.All(x => it.Methods.Any(y => x.Key.Equals(y.Key)));
        }

        public InterfaceType GetInstance() => new InterfaceType(this, false);

        public InterfaceType GetSuperInstance() => new InterfaceType(this, true) { Constant = true } ;

        protected override bool _equals(DataType other)
        {
            if (other is InterfaceType it)
            {
                if (_initialized != it._initialized || SuperForm != it.SuperForm)
                    return false;

                return Methods.DictionaryEquals(it.Methods);
            }

            return false;
        }

        // tests if a type implements all necessary methods to be a child of this interface
        public bool Derive(DataType child, bool allowDirectDerivation=false)
        {
            InterfaceType interf;

            if (SuperForm)
                return false;
            else if (child is InterfaceType it)
            {
                if (allowDirectDerivation)
                    interf = it;
                else
                    return false;
            }
            else
                interf = child.GetInterface();

            if (Methods.Where(x => !x.Value).All(x => interf.Methods.Select(y => y.Key).Where(y => x.Key.Equals(y)).Count() > 0)) {
                foreach (var method in Methods) {
                    if (method.Value && !interf.Methods.Contains(method))
                        interf.AddMethod(method.Key, method.Value);
                }

                interf.Implements.Add(this);

                return true;
            }

            return false;
        }

        public override DataType ConstCopy()
            => new InterfaceType(this, SuperForm)
                {
                    _initialized = _initialized,
                    Constant = true
                };

        public override string ToString() => RemovePrefix(Name);
    }
}
