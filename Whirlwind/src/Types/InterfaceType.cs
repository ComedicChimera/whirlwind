using Whirlwind.Semantic;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class InterfaceType : DataType
    {
        public readonly List<InterfaceType> Implements;
        public bool SuperForm { get; private set; } = false;

        private readonly Dictionary<Symbol, bool> _methods;
        private bool _initialized = false;

        public InterfaceType()
        {
            Implements = new List<InterfaceType>();

            _methods = new Dictionary<Symbol, bool>();

            Constant = true;
        }

        private InterfaceType(InterfaceType interf, bool superForm)
        {
            Implements = interf.Implements;

            if (superForm)
            {
                SuperForm = true;
                _methods = new Dictionary<Symbol, bool>();

                foreach (var method in interf._methods)
                {
                    if (method.Value)
                        _methods.Add(method.Key, method.Value);
                }
            }
            else
                _methods = interf._methods;

            _initialized = true;
        }

        public bool AddMethod(Symbol fn, bool hasBody)
        {
            if (!_methods.Any(x => x.Key.Name == fn.Name))
            {
                _methods.Add(fn, hasBody);

                return true;
            }
            else if (fn.DataType is FunctionType fta)
            {
                Symbol symbol = _methods.Where(x => x.Key.Name == fn.Name).First().Key;

                if (symbol.DataType is FunctionType ftb && FunctionGroup.CanDistinguish(fta, ftb))
                {
                    symbol.DataType = new FunctionGroup(new List<FunctionType> { fta, ftb });
                    return true;
                }
                else if (symbol.DataType is FunctionGroup)
                    return ((FunctionGroup)symbol.DataType).AddFunction((FunctionType)fn.DataType);
            }

            return false;
        }

        public bool GetFunction(string fnName, out Symbol symbol)
        {
            if (_methods.Select(x => x.Key.Name).Contains(fnName))
            {
                symbol = _methods.Keys.Where(x => x.Name == fnName).ToArray()[0];
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
            else
            {
                var interf = other.GetInterface();

                return MatchInterface(interf);
            }
        }

        private bool MatchInterface(InterfaceType it)
        {
            if (_methods.Count != it._methods.Count)
                return false;

            return _methods.All(x => it._methods.Any(y => x.Key.Equals(y.Key)));
        }

        public InterfaceType GetInstance() => new InterfaceType(this, false);

        public InterfaceType GetSuperInstance() => new InterfaceType(this, true) { Constant = true } ;

        protected override bool _equals(DataType other)
        {
            if (other is InterfaceType it)
            {
                if (_initialized != it._initialized || SuperForm != it.SuperForm)
                    return false;

                return _methods.SequenceEqual(it._methods);
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

            if (_methods.Where(x => !x.Value).All(x => interf._methods.Select(y => y.Key).Where(y => x.Key.Equals(y)).Count() > 0)) {
                foreach (var method in _methods) {
                    if (method.Value && !interf._methods.Contains(method))
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
    }
}
