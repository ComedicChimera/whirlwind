using Whirlwind.Types;
using Whirlwind.Parser;
using Whirlwind.Semantic.Visitor;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Checker
{
    partial class Checker
    {
        // takes in rootType by reference so it can handle overloads / type mutations
        public static void CheckOperand(ref DataType rootType, DataType operandType, string op, TextPosition position)
        {
            // generic placeholders have no bearing on anything as they are just placeholders
            if (rootType is GenericPlaceholder || operandType is GenericPlaceholder)
                return;

            if (HasOverload(rootType, $"__{op}__", new ArgumentList(new List<DataType>() { operandType }),
                            out DataType returnType))
            {
                rootType = returnType;
                return;
            }

            bool valid = false;
            switch (op)
            {
                case "+":
                    {
                        var stringType = new SimpleType(SimpleType.SimpleClassifier.STRING);

                        if (Numeric(operandType))
                            valid = true;
                        else if (stringType.Coerce(rootType) && stringType.Coerce(operandType))
                        {
                            rootType = stringType;
                            valid = true;
                        } 
                        else if (new[] { TypeClassifier.ARRAY, TypeClassifier.LIST }.Contains(operandType.Classify()))
                            valid = true;
                        else if (rootType.Classify() == TypeClassifier.POINTER && new SimpleType(SimpleType.SimpleClassifier.INTEGER).Coerce(operandType))
                            return;
                    }
                    break;
                case "*":
                case "-":
                case "/":
                case "~/":
                case "%":
                case "~^":
                    {
                        if (Numeric(operandType))
                        {
                            valid = true;
                            break;
                        }
                        if (rootType.Classify() == TypeClassifier.POINTER && new SimpleType(SimpleType.SimpleClassifier.INTEGER).Coerce(operandType))
                            return;
                    }
                    break;
                case ">>":
                case "<<":
                    {
                        if (rootType.Classify() == TypeClassifier.SIMPLE && new SimpleType(SimpleType.SimpleClassifier.INTEGER).Coerce(operandType))
                            return;
                    }
                    break;
                case "==":
                case "!=":
                    {
                        // all that is needed is operator congruence
                        valid = true;
                    }
                    break;
                case ">":
                case "<":
                case ">=":
                case "<=":
                    {
                        if (Numeric(operandType) || (rootType.Classify() == TypeClassifier.POINTER && operandType.Classify() == TypeClassifier.POINTER))
                        {
                            rootType = new SimpleType(SimpleType.SimpleClassifier.BOOL);
                            return;
                        }  
                    }
                    break;
                case "AND":
                case "OR":
                case "XOR":
                    {
                        if (operandType.Classify() == TypeClassifier.SIMPLE && rootType.Classify() == TypeClassifier.SIMPLE)
                        {
                            SimpleType simpleRoot = (SimpleType)rootType, simpleOperand = (SimpleType)operandType;

                            if (simpleRoot.Type == SimpleType.SimpleClassifier.BOOL && simpleOperand.Type == SimpleType.SimpleClassifier.BOOL)
                                return;

                            valid = true;
                        }
                    }
                    break;
            }

            if (valid)
            {
                if (rootType.Classify() == TypeClassifier.VOID)
                    throw new SemanticException("Unable to apply operator to root type of void", position);

                if (!rootType.Coerce(operandType))
                {
                    if (operandType.Coerce(rootType))
                        rootType = operandType;
                    else
                        throw new SemanticException($"All operands of the '{op}' operator must be of similar types", position);
                }

                // all roots that reach this point are simple
                if (op == "/")
                    rootType = new SimpleType(_large((SimpleType)rootType) ? SimpleType.SimpleClassifier.DOUBLE : SimpleType.SimpleClassifier.FLOAT);
                else if (op == "~/")
                    rootType = new SimpleType(_large((SimpleType)rootType) ? SimpleType.SimpleClassifier.LONG : SimpleType.SimpleClassifier.INTEGER);
                else if (op == "==" || op == "!=")
                    rootType = new SimpleType(SimpleType.SimpleClassifier.BOOL);
                else if (op == "-")
                    // convert all operands in a subtraction operation to signed
                    rootType = new SimpleType(((SimpleType)rootType).Type);
            }       
            else
                throw new SemanticException($"Invalid operands for '{op}' operator", position);
        }

        static private bool _large(SimpleType rootType)
        {
            if (new[] { SimpleType.SimpleClassifier.LONG, SimpleType.SimpleClassifier.DOUBLE }.Contains(rootType.Type))
                return true;

            return false;
        }
    }
}
