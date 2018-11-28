using Whirlwind.Semantic;
using Whirlwind.Parser;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class InterfaceType : IDataType
    {
        private readonly List<Symbol> _functions;
        private readonly List<ASTNode> _bodies;

        bool initialized = false;

        public InterfaceType()
        {
            _functions = new List<Symbol>();
            _bodies = new List<ASTNode>();
        }

        public bool AddFunction(Symbol fn)
        {
            if (!_functions.Contains(fn))
            {
                _functions.Add(fn);
                return true;
            }
            return false;
        }

        public bool AddFunction(Symbol fn, ASTNode body)
        {
            if (!AddFunction(fn))
                return false;

            _bodies.Add(body);
            return true;
        }

        public bool GetFunction(string fnName, out Symbol symbol)
        {
            if (_functions.Select(x => x.Name).Contains(fnName))
            {
                symbol = _functions.Where(x => x.Name == fnName).ToArray()[0];
                return true;
            }

            symbol = null;
            return false;
        }

        public bool MatchObject(ObjectType obj)
        {
            var objInstance = obj.GetInstance();
            foreach (Symbol fn in _functions)
            {
                if (objInstance.GetProperty(fn.Name, out Symbol match))
                {
                    if (!fn.DataType.Coerce(match.DataType))
                        return false;
                }
                else return false;
            }

            return true;
        }

        public TypeClassifier Classify() => initialized ? TypeClassifier.INTERFACE_INSTANCE : TypeClassifier.INTERFACE;

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == TypeClassifier.OBJECT_INSTANCE)
            {
                if (((ObjectInstance)other).Inherits.Contains(this))
                {
                    return true;
                }
                foreach (Symbol function in _functions)
                {
                    if (((ObjectInstance)other).GetProperty(function.Name, out Symbol matchedFunction))
                    {
                        if (!function.DataType.Coerce(matchedFunction.DataType)) return false;
                    }
                    else return false;
                }
                return true;
            }
            return false;
        }

        public void Initialize() =>
            initialized = true;

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.INTERFACE || other.Classify() == TypeClassifier.INTERFACE_INSTANCE)
            {
                InterfaceType it = (InterfaceType)other;

                if (initialized != it.initialized)
                    return false;

                if (_functions.Count == it._functions.Count)
                {
                    for (int i = 0; i < _functions.Count; i++)
                    {
                        if (_functions[i].Name != it._functions[i].Name || !_functions[i].Equals(it._functions[i]))
                            return false;
                    }
                }
            }

            return false;
        }
    }
}
