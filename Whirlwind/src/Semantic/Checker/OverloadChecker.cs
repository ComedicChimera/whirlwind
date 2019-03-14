using Whirlwind.Types;

namespace Whirlwind.Semantic.Checker
{
    partial class Checker
    {
        public static bool HasOverload(DataType type, string methodName, out DataType returnType)
        {
            if (type.Classify() != TypeClassifier.INTERFACE)
            {
                if (type.GetInterface().GetFunction(methodName, out Symbol symbol))
                {
                    // ADD GENERIC CASE
                    if (((FunctionType)symbol.DataType).MatchArguments(new ArgumentList()))
                    {
                        returnType = ((FunctionType)symbol.DataType).ReturnType;
                        return true;
                    }
                }
            }

            returnType = new SimpleType();
            return false;
        }

        public static bool HasOverload(DataType type, string methodName, ArgumentList arguments, out DataType returnType)
        {
            if (type.Classify() != TypeClassifier.INTERFACE)
            {
                if (type.GetInterface().GetFunction(methodName, out Symbol symbol))
                {
                    // ADD GENERIC CASE
                    if (((FunctionType)symbol.DataType).MatchArguments(arguments))
                    {
                        returnType = ((FunctionType)symbol.DataType).ReturnType;
                        return true;
                    }
                }
            }

            returnType = new SimpleType();
            return false;
        }
    }
}
