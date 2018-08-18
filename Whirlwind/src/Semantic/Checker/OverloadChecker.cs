using Whirlwind.Types;

using System.Collections.Generic;

namespace Whirlwind.Semantic.Checker
{
    partial class Checker
    {
        public static bool HasOverload(IDataType type, string methodName, out IDataType returnType)
        {
            if (type.Classify() == "MODULE_INSTANCE")
            {
                if (((ModuleInstance)type).GetProperty(methodName, out Symbol method))
                {
                    if (method.DataType.Classify() == "FUNCTION")
                    {
                        if (CheckParameters((FunctionType)method.DataType, new List<ParameterValue>()).IsError)
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

        public static bool HasOverload(IDataType type, string methodName, List<ParameterValue> parameters, out IDataType returnType)
        {
            if (type.Classify() == "MODULE_INSTANCE")
            {
                if (((ModuleInstance)type).GetProperty(methodName, out Symbol method))
                {
                    if (method.DataType.Classify() == "FUNCTION")
                    {
                        if (CheckParameters((FunctionType)method.DataType, parameters).IsError)
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
