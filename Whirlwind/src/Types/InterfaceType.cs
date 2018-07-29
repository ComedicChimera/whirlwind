using Whirlwind.Semantic;

using System.Collections.Generic;

namespace Whirlwind.Types
{
    class InterfaceType : IDataType
    {
        private readonly List<Symbol> _functions;

        public InterfaceType()
        {
            _functions = new List<Symbol>();
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

        public bool MatchModule(ModuleType module)
        {
            var moduleInstance = module.GetInstance();
            foreach(Symbol fn in _functions)
            {
                if (moduleInstance.GetProperty(fn.Name, out Symbol match))
                {
                    if (!fn.DataType.Coerce(match.DataType))
                        return false;
                }
                else return false;
            }

            return true;
        }

        public string Classify() => "INTERFACE";

        public bool Coerce(IDataType other)
        {
            if (other.Classify() == "MODULE_INSTANCE")
            {
                if (((ModuleInstance)other).Inherits.Contains(this))
                {
                    return true;
                }
                foreach(Symbol function in _functions)
                {
                    if (((ModuleInstance)other).GetProperty(function.Name, out Symbol matchedFunction))
                    {
                        if (!function.DataType.Coerce(matchedFunction.DataType)) return false;
                    }
                    else return false;
                }
                return true;
            }
            return false;
        }
    }
}
