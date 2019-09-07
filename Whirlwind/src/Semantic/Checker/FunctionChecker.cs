using System.Linq;

using Whirlwind.Types;

namespace Whirlwind.Semantic.Checker
{
    static partial class Checker
    {
        // data about parameter check
        public struct ArgumentCheckData
        {
            public bool IsError;
            public string ErrorMessage;
            public int ParameterPosition;

            public ArgumentCheckData(string errorMessage, int paramPos)
            {
                IsError = true;
                ErrorMessage = errorMessage;
                ParameterPosition = paramPos;
            }
        }
    
        public static ArgumentCheckData CheckArguments(FunctionType fn, ArgumentList args)
        {
            var parameterDictionary = fn.Parameters.ToDictionary(x => x.Name);

            int position = 0;
            var setParameters = Enumerable.Repeat(false, fn.Parameters.Count).ToDictionary(x => fn.Parameters[position++].Name);

            position = 0;
            foreach (var param in args.UnnamedArguments)
            {
                if (position >= fn.Parameters.Count)
                    return new ArgumentCheckData("Too many parameters for the given function", -1);

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
                {
                    if (fnParameter.Name.StartsWith("$arg"))
                        return new ArgumentCheckData($"Invalid type for unnamed argument at position {fnParameter.Name.Substring(4)}", 
                            position);
                    
                    return new ArgumentCheckData($"Invalid type for parameter named `{fnParameter.Name}`", position);
                }
                    
            }

            foreach (var namedParam in args.NamedArguments)
            {
                if (!fn.Parameters.Select(x => x.Name).Contains(namedParam.Key))
                    return new ArgumentCheckData($"Parameter `{namedParam.Key}` doesn't exist", position);
                else if (setParameters[namedParam.Key])                    
                    return new ArgumentCheckData("Unable to set multiple values for one parameters", position);

                var match = fn.Parameters.Where(x => x.Name == namedParam.Key).First();

                if (!match.DataType.Coerce(namedParam.Value) || match.Indefinite)
                    return new ArgumentCheckData($"Unable to specify given value for the parameter `{namedParam.Key}`", position);

                setParameters[namedParam.Key] = true;

                position++;
            }

            foreach (var param in setParameters)
            {
                if (!param.Value && !(parameterDictionary[param.Key].Optional || parameterDictionary[param.Key].Indefinite))
                    return new ArgumentCheckData($"No value specified for mandatory parameter `{param.Key}`", -1);
            }

            return new ArgumentCheckData();
        }
    }
}
