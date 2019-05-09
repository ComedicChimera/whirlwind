using Whirlwind.Semantic;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class InterfaceType : DataType
    {
        public readonly List<InterfaceType> Implements;

        private readonly Dictionary<Symbol, bool> _methods;

        bool initialized = false;

        public InterfaceType()
        {
            Implements = new List<InterfaceType>();

            _methods = new Dictionary<Symbol, bool>();

            Constant = true;
        }

        private InterfaceType(InterfaceType interf)
        {
            Implements = new List<InterfaceType>();

            _methods = interf._methods;
            initialized = true;
        }

        public bool AddMethod(Symbol fn, bool hasBody)
        {
            if (!_methods.Any(x => x.Key.Name == fn.Name))
            {
                _methods.Add(fn, hasBody);

                return true;
            }
            else if (fn.DataType is FunctionType)
            {
                Symbol symbol = _methods.Where(x => x.Key.Name == fn.Name).First().Key;

                if (symbol.DataType is FunctionType && !symbol.DataType.Coerce(fn.DataType))
                {
                    symbol.DataType = new FunctionGroup(new List<FunctionType> { (FunctionType)symbol.DataType, (FunctionType)fn.DataType });
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

        public override TypeClassifier Classify() => initialized ? TypeClassifier.INTERFACE_INSTANCE : TypeClassifier.INTERFACE;

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

        public InterfaceType GetInstance() => new InterfaceType(this);

        public override bool Equals(DataType other)
        {
            if (other.Classify() == TypeClassifier.INTERFACE || other.Classify() == TypeClassifier.INTERFACE_INSTANCE)
            {
                InterfaceType it = (InterfaceType)other;

                if (initialized != it.initialized)
                    return false;

                if (_methods.Count == it._methods.Count)
                {
                    using (var e1 = _methods.GetEnumerator())
                    using (var e2 = it._methods.GetEnumerator())
                    {
                        while (e1.MoveNext() && e2.MoveNext())
                        {
                            if (!e1.Current.Key.Equals(e2.Current.Key) || e1.Current.Value != e2.Current.Value)
                                return false;
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        // tests if a type implements all necessary methods to be a child of this interface
        public bool Derive(DataType child)
        {
            if (child is InterfaceType)
                return false;

            var interf = child.GetInterface();

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
            => new InterfaceType(this)
                {
                    initialized = initialized,
                    Constant = true
                };
    }
}
