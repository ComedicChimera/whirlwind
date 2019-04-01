using Whirlwind.Types;

using System.Collections.Generic;

namespace Whirlwind.Semantic.Checker
{
    partial class Checker
    {
        public static bool HasOverload(DataType type, string methodName, out DataType returnType)
            => HasOverload(type, methodName, new ArgumentList(), out returnType);

        public static bool HasOverload(DataType type, string methodName, ArgumentList arguments, out DataType returnType)
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
                                returnType = ((FunctionType)symbol.DataType).ReturnType;
                                return true;
                            }
                            break;
                        case TypeClassifier.FUNCTION_GROUP:
                            if (((FunctionGroup)symbol.DataType).GetFunction(arguments, out FunctionType ft))
                            {
                                returnType = ft.ReturnType;
                                return true;
                            }
                            break;
                        case TypeClassifier.TEMPLATE:
                            {
                                TemplateType tt = (TemplateType)symbol.DataType;

                                if (tt.DataType is FunctionType && tt.Infer(arguments, out List<DataType> inferredTypes))
                                {
                                    tt.CreateTemplate(inferredTypes, out DataType dt);

                                    returnType = ((FunctionType)dt).ReturnType;
                                    return true;
                                }
                            }                            
                            break;
                    }
                    
                }
            }

            returnType = new SimpleType();
            return false;
        }
    }
}
