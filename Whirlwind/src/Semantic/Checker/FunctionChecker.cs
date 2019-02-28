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
    
        public static ParameterCheckData CheckParameters(FunctionType fn, ArgumentList args)
        {
            var parameterDictionary = fn.Parameters.ToDictionary(x => x.Name);

            int position = 0;
            var setParameters = Enumerable.Repeat(false, fn.Parameters.Count).ToDictionary(x => fn.Parameters[position++].Name);

            position = 0;
            foreach (var param in args.UnnamedArguments)
            {
                if (position >= fn.Parameters.Count)
                    return new ParameterCheckData("Too many parameters for the given function", -1);

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

            foreach (var namedParam in args.NamedArguments)
            {
                position++;

                if (!fn.Parameters.Select(x => x.Name).Contains(namedParam.Key))
                    return new ParameterCheckData($"Parameter `{namedParam.Key}` doesn't exist", position);
                else if (setParameters[namedParam.Key])                    
                    return new ParameterCheckData("Unable to set multiple values for one parameters", position);
                else if (!fn.Parameters.Where(x => x.Name == namedParam.Key).First().DataType.Coerce(namedParam.Value))
                    return new ParameterCheckData($"Invalid type for parameters `{namedParam.Key}", position);

                setParameters[namedParam.Key] = true;
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
