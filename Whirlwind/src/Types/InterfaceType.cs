using Whirlwind.Semantic;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class InterfaceType : DataType, IDataType
    {
        private readonly Dictionary<Symbol, bool> _methods;

        bool initialized = false;

        public InterfaceType()
        {
            _methods = new Dictionary<Symbol, bool>();
        }

        private InterfaceType(InterfaceType interf)
        {
            _methods = interf._methods;
            initialized = true;
        }

        public bool AddFunction(Symbol fn, bool hasBody)
        {
            if (!_methods.ContainsKey(fn))
            {
                _methods.Add(fn, hasBody);

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

            symbol = null;
            return false;
        }

        public bool MatchObject(ObjectType obj)
        {
            var objInstance = obj.GetInternalInstance();
            foreach (var fn in _methods)
            {
                if (fn.Value)
                    continue;

                if (objInstance.GetMember(fn.Key.Name, out Symbol match))
                {
                    if (!fn.Key.DataType.Coerce(match.DataType) || _wrongModifiers(fn.Key.Modifiers, match.Modifiers))
                        return false;
                }
                else return false;
            }

            return true;
        }

        private bool _wrongModifiers(List<Modifier> interfMod, List<Modifier> objMod)
            => objMod.Contains(Modifier.PRIVATE) && !interfMod.Contains(Modifier.PRIVATE);

        public TypeClassifier Classify() => initialized ? TypeClassifier.INTERFACE_INSTANCE : TypeClassifier.INTERFACE;

        protected sealed override bool _coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.OBJECT_INSTANCE)
            {
                if (((ObjectType)other).Inherits.Contains(this))
                    return true;

                return MatchObject((ObjectType)other);
            }
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
                }
            }

            return false;
        }

        // tests if it is possible for the child to be a derivation of the parent interface
        // and gives any methods to the child that are implemented within the interface
        public bool Derive(ObjectType child)
        {
            if (MatchObject(child))
            {
                foreach (var method in _methods)
                {
                    if (method.Value)
                        // fails silently if it was unable to add member (allows for overriding)
                        child.AddMember(method.Key);
                }

                return true;
            }

            return false;
        }
    }
}
