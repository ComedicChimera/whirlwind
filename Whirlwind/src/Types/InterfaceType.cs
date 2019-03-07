using Whirlwind.Semantic;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class InterfaceType : DataType, IDataType
    {
        private readonly Dictionary<Symbol, bool> _methods;
        private readonly Dictionary<Symbol, bool> _methodTemplates;

        bool initialized = false;

        public InterfaceType()
        {
            _methods = new Dictionary<Symbol, bool>();
            _methodTemplates = new Dictionary<Symbol, bool>();
        }

        private InterfaceType(InterfaceType interf)
        {
            _methods = interf._methods;
            _methodTemplates = interf._methodTemplates;
            initialized = true;
        }

        public bool AddMethod(Symbol fn, bool hasBody)
        {
            if (!_methods.ContainsKey(fn))
            {
                _methods.Add(fn, hasBody);

                return true;
            }
            return false;
        }

        public bool AddTemplate(Symbol fn, bool hasBody)
        {
            if (!_methodTemplates.ContainsKey(fn))
            {
                _methodTemplates.Add(fn, hasBody);
                return true;
            }

            return false;
        }

        public bool GetFunction(string fnName, out Symbol symbol)
        {
            if (_methods.Select(x => x.Key.Name).Contains(fnName))
            {
                symbol = _methods.Keys.Where(x => x.Name == fnName).ToArray()[0];
                return !symbol.Modifiers.Contains(Modifier.PRIVATE);
            }
            else if (_methodTemplates.Select(x => x.Key.Name).Contains(fnName))
            {
                symbol = _methodTemplates.Keys.Where(x => x.Name == fnName).ToArray()[0];
                return !symbol.Modifiers.Contains(Modifier.PRIVATE);
            }

            symbol = null;
            return false;
        }

        private bool _wrongModifiers(List<Modifier> interfMod, List<Modifier> objMod)
            => objMod.Contains(Modifier.PRIVATE) && !interfMod.Contains(Modifier.PRIVATE);

        public TypeClassifier Classify() => initialized ? TypeClassifier.INTERFACE_INSTANCE : TypeClassifier.INTERFACE;

        protected sealed override bool _coerce(IDataType other)
        {
            var interf = other.GetInterface();

            // add coercion checking
            return false;
        }

        public InterfaceType GetInstance() => new InterfaceType(this);

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.INTERFACE || other.Classify() == TypeClassifier.INTERFACE_INSTANCE)
            {
                InterfaceType it = (InterfaceType)other;

                if (initialized != it.initialized)
                    return false;

                if (_methods.Count == it._methods.Count && _methodTemplates.Count == it._methodTemplates.Count)
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

                    using (var e1 = _methodTemplates.GetEnumerator())
                    using (var e2 = it._methodTemplates.GetEnumerator())
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
        public bool Derive(IDataType child)
        {
            var interf = child.GetInterface();

            if (_methods.Where(x => !x.Value).All(x => interf._methods.Contains(x)) &&
            _methodTemplates.Where(x => !x.Value).All(x => interf._methodTemplates.Contains(x))) {
                foreach (var method in _methods) {
                    if (method.Value && !interf._methods.Contains(method))
                        interf.AddMethod(method.Key, method.Value);
                }

                foreach (var methodTemplate in _methodTemplates) {
                    if (methodTemplate.Value && !interf._methodTemplates.Contains(methodTemplate))
                        interf.AddTemplate(methodTemplate.Key, methodTemplate.Value);
                }

                return true;
            }

            return false;
        }
    }
}
