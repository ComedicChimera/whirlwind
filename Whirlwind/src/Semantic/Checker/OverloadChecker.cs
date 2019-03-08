using Whirlwind.Types;

using System.Collections.Generic;

namespace Whirlwind.Semantic.Checker
{
    partial class Checker
    {
        public static bool HasOverload(DataType type, string methodName, out DataType returnType)
        {
            if (type.Classify() == TypeClassifier.OBJECT_INSTANCE)
            {
                if (((ObjectType)type).GetMember(methodName, out Symbol method))
                {
                    if (method.DataType.Classify() == TypeClassifier.FUNCTION)
                    {
                        if (CheckArguments((FunctionType)method.DataType, new ArgumentList()).IsError)
                        {
                            returnType = new SimpleType();
                            return false;
                        }
                            
                        returnType = ((FunctionType)method.DataType).ReturnType;
                        return true;
                    }             
                }
            }
            returnType = new SimpleType();
            return false;
        }

        public static bool HasOverload(DataType type, string methodName, ArgumentList arguments, out DataType returnType)
        {
            if (type.Classify() == TypeClassifier.OBJECT_INSTANCE)
            {
                if (((ObjectType)type).GetMember(methodName, out Symbol method))
                {
                    if (method.DataType.Classify() == TypeClassifier.FUNCTION)
                    {
                        if (CheckArguments((FunctionType)method.DataType, arguments).IsError)
                        {
                            returnType = new SimpleType();
                            return false;
                        }
                           
                        returnType = ((FunctionType)method.DataType).ReturnType;
                        return true;
                    }
                }
            }
            returnType = new SimpleType();
            return false;
        }
    }
}
