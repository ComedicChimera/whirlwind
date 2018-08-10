using Whirlwind.Types;
using Whirlwind.Parser;
using Whirlwind.Semantic.Visitor;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Checker
{
    partial class Checker
    {
        // takes in rootType by reference so it can handle overloads
        public static void CheckOperand(ref IDataType rootType, IDataType operandType, string op, TextPosition position)
        {
            bool valid = false;
            switch (op)
            {
                case "^":
                    if (Numeric(operandType))
                        valid = true;
                    else if (HasOverload(rootType, "__pow__", new List<ParameterValue>() { new ParameterValue(operandType) }, out IDataType returnType))
                    {
                        rootType = returnType;
                        valid = true;
                    }
                    break;
            }

            if (valid)
            {
                if (!rootType.Coerce(operandType))
                {
                    if (operandType.Coerce(rootType))
                        rootType = operandType;
                    else
                        throw new SemanticException($"All operands of the '{op}' operator must be of similar types", position);
                }
            }
            else
                throw new SemanticException($"Invalid operands for '{op}' operator", position);
        }
    }
}
