using System.Collections.Generic;
using System.Linq;

using Whirlwind.Semantic;

namespace Whirlwind.Types
{
    enum MethodStatus
    {
        ABSTRACT,
        VIRTUAL,
        IMPLEMENTED
    }

    class InterfaceType : DataType
    {
        public readonly string Name;
        public readonly List<InterfaceType> Implements;
        public bool SuperForm { get; private set; } = false;
        public Dictionary<Symbol, MethodStatus> Methods { get; private set; }

        private bool _initialized = false;

        public InterfaceType(string name)
        {
            Name = name;

            Implements = new List<InterfaceType>();

            Methods = new Dictionary<Symbol, MethodStatus>();

            Constant = true;
        }

        private InterfaceType(InterfaceType interf, bool superForm)
        {
            Name = interf.Name;

            Implements = interf.Implements;

            if (superForm)
            {
                SuperForm = true;
                Methods = new Dictionary<Symbol, MethodStatus>();

                foreach (var method in interf.Methods)
                {
                    if (method.Value == MethodStatus.VIRTUAL)
                        Methods.Add(method.Key, method.Value);
                }
            }
            else
                Methods = interf.Methods;

            _initialized = true;
        }

        public bool AddMethod(Symbol fn, MethodStatus methodStatus)
        {
            if (Methods.All(x => x.Key.Name != fn.Name || _distinguish(x.Key.DataType, fn.DataType)))
            {
                Methods.Add(fn, methodStatus);

                return true;
            }

            return false;
        }

        private bool _distinguish(DataType a, DataType b)
        {
            if (a is FunctionType fta && b is FunctionType ftb)
                return FunctionGroup.CanDistinguish(fta, ftb);
            else if (a is GenericType gta && b is GenericType gtb)
                // we know gta and gtb are both generic functions (cast is always valid)
                return FunctionGroup.CanDistinguish((FunctionType)gta.DataType, (FunctionType)gtb.DataType);

            // if they are of different types with same name, then although they are different, the language
            // can't distinguish them properly nor can it group them so we return false to create an invalid case
            return false;
        }

        public bool GetFunction(string fnName, out Symbol symbol)
        {
            if (Methods.Select(x => x.Key.Name).Contains(fnName))
            {
                var matches = Methods.Keys.Where(x => x.Name == fnName);

                if (matches.Count() == 1)
                {
                    symbol = Methods.Keys.Where(x => x.Name == fnName).First();
                    return true;
                }

                // create artificial function groups so that interfaces have the right behavior
                // that is GetFunction will return the corresponding function group if there are matches
                // this allows interfaces to integrate with the rest of semantics of the language while still
                // preserving the ability to have different members of a function or generic group have 
                // different implementation states (ie. some implemented, some not) something just storing them
                // as groups in the Methods list would not allow
                else if (matches.First().DataType is FunctionType)
                {
                    string name = matches.First().Name;
                    symbol = new Symbol(name, new FunctionGroup(name, matches.Select(x => (FunctionType)x.DataType).ToList()));
                    return true;
                }
                // assume generic group
                else
                {
                    string name = matches.First().Name;
                    symbol = new Symbol(name, new GenericGroup(name, matches.Select(x => (GenericType)x.DataType).ToList()));
                    return true;
                }
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
                // TODO: should name be checked?

                if (_initialized != it._initialized || SuperForm != it.SuperForm)
                    return false;

                return Methods.DictionaryEquals(it.Methods);
            }

            return false;
        }

        public override bool GenerateEquals(DataType other)
        {
            if (other == null)
                return false;

            if (other is InterfaceType it)
            {
                // TODO: should name be checked?
                if (Name != it.Name)
                    return false;

                // TODO: check for superform?

                return it.Methods.DictionaryEquals(it.Methods);
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

            if (Methods.Where(x => x.Value == MethodStatus.ABSTRACT)
                .All(x => interf.Methods.Select(y => y.Key).Where(y => x.Key.Equals(y)).Count() > 0))
            {
                foreach (var method in Methods) {
                    if (method.Value != MethodStatus.ABSTRACT && !interf.Methods.Contains(method))
                        interf.AddMethod(method.Key, MethodStatus.VIRTUAL);
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

        public override string ToString() => Name;
    }
}
