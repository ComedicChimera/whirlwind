using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Types
{
    class FunctionGroup : IDataType
    {
        private List<FunctionType> _functions;

        public FunctionGroup(FunctionType a, FunctionType b)
        {
            _functions = new List<FunctionType>() { a, b };
        }

        public bool AddFunction(FunctionType fn)
        {
            var fnParams = fn.Parameters.Select(x => x.DataType).ToList();

            // check both directions (overloading is more explicit in Whirlwind)
            if (!_functions.Any(x => x.MatchParameters(fnParams) || fn.MatchParameters(x.Parameters.Select(y => y.DataType).ToList())))
            {
                _functions.Add(fn);
                return false;
            }

            return true;
        }

        public bool GetFunction(List<IDataType> args, out FunctionType outFn)
        {
            foreach (var fn in _functions)
            {
                if (fn.MatchParameters(args))
                {
                    outFn = fn;
                    return true;
                }
            }

            outFn = null;
            return false;
        }

        public bool Coerce(IDataType other) => Equals(other);

        public bool Equals(IDataType other)
        {
            if (other.Classify() == TypeClassifier.FUNCTION_GROUP)
            {
                FunctionGroup og = (FunctionGroup)other;

                if (_functions.Count == og._functions.Count)
                    return Enumerable.Range(0, _functions.Count).All(i => _functions[i].Equals(og._functions[i]));
            }

            // regular functions don't count since function groups are only ever composed of 2 or more functions
            return false;
        }

        public TypeClassifier Classify() => TypeClassifier.FUNCTION_GROUP;
    }
}
