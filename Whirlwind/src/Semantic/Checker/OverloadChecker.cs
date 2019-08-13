using Whirlwind.Types;

using System.Collections.Generic;

namespace Whirlwind.Semantic.Checker
{
    partial class Checker
    {
        public static bool HasOverload(DataType type, string methodName, out DataType returnType)
        {
            if (GetOverload(type, methodName, new ArgumentList(), out FunctionType fnType))
            {
                returnType = fnType.ReturnType;
                return true;
            }

            returnType = null;
            return false;
        }

        public static bool HasOverload(DataType type, string methodName, ArgumentList arguments, out DataType returnType)
        {
            if (GetOverload(type, methodName, arguments, out FunctionType fnType))
            {
                returnType = fnType.ReturnType;
                return true;
            }

            returnType = null;
            return false;
        }

        public static bool GetOverload(DataType type, string methodName, ArgumentList arguments, out FunctionType fnType)
        {
            if (type.Classify() != TypeClassifier.INTERFACE)
            {
                if (type.GetInterface().GetFunction(methodName, out Symbol symbol))
                {
                    switch (symbol.DataType.Classify())
                    {
                        case TypeClassifier.FUNCTION:
                            if (((FunctionType)symbol.DataType).MatchArguments(arguments))
                            {
                                fnType = (FunctionType)symbol.DataType;
                                return true;
                            }
                            break;
                        case TypeClassifier.FUNCTION_GROUP:
                            if (((FunctionGroup)symbol.DataType).GetFunction(arguments, out FunctionType ft))
                            {
                                fnType = (FunctionType)ft;
                                return true;
                            }
                            break;
                        case TypeClassifier.GENERIC:
                            {
                                GenericType tt = (GenericType)symbol.DataType;

                                if (tt.DataType is FunctionType && tt.Infer(arguments, out List<DataType> inferredTypes))
                                {
                                    tt.CreateGeneric(inferredTypes, out DataType dt);

                                    fnType = (FunctionType)dt;
                                    return true;
                                }
                            }
                            break;
                        case TypeClassifier.GENERIC_GROUP:
                            if (((GenericGroup)symbol.DataType).GetFunction(arguments, out FunctionType result))
                            {
                                fnType = result;
                                return true;
                            }
                            break;
                    }

                }
            }

            fnType = null;
            return false;
        }
    }
}
