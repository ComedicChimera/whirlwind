using Whirlwind.Types;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Checker
{
    static partial class Checker
    {
        // data about parameter check
        public struct ParameterCheckData
        {
            public bool IsError;
            public string ErrorMessage;
            public int ParameterPosition;

            public ParameterCheckData(string errorMessage, int paramPos)
            {
                IsError = true;
                ErrorMessage = errorMessage;
                ParameterPosition = paramPos;
            }
        }

        public static ParameterCheckData CheckParameters(FunctionType fn, List<IDataType> values)
        {
            var parameterDictionary = fn.Parameters.ToDictionary(x => x.Name);

            int position = 0;
            var setParameters = Enumerable.Repeat(false, fn.Parameters.Count).ToDictionary(x => fn.Parameters[position++].Name);

            position = 0;
            foreach (var param in values)
            {
                var fnParameter = fn.Parameters[position];

                if (fnParameter.DataType.Coerce(param) && !setParameters[fnParameter.Name])
                {
                    if (!fnParameter.Indefinite)
                    {
                        setParameters[fnParameter.Name] = true;
                        position++;
                    }
                }
                else
                    return new ParameterCheckData($"Invalid type for parameter `{fnParameter.Name}`", position);
            }

            foreach (var param in setParameters)
            {
                if (!param.Value && !(parameterDictionary[param.Key].Optional || parameterDictionary[param.Key].Indefinite))
                    return new ParameterCheckData($"No value specified for mandatory parameter `{param.Key}`", -1);
            }

            return new ParameterCheckData();
        }
    }
}
