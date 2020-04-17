using System.Collections.Generic;
using System.Linq;

using Whirlwind.Types;
using Whirlwind.Syntax;

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
                        var stringType = new SimpleType(SimpleType.SimpleClassifier.STRING, true);

                        if (rootType is PointerType pt && !pt.IsDynamicPointer && new SimpleType(SimpleType.SimpleClassifier.INTEGER, true)
                            .Coerce(operandType))
                        {
                            return;
                        }                           
                        else if (Numeric(operandType))
                            valid = true;
                        else if (stringType.Coerce(rootType) && stringType.Coerce(operandType))
                        {
                            rootType = stringType;
                            valid = true;
                        } 
                        else if (new[] { TypeClassifier.ARRAY, TypeClassifier.LIST }.Contains(operandType.Classify()))
                            valid = true;
                    }
                    break;
                case "-":
                    {
                        if (rootType is PointerType pt && !pt.IsDynamicPointer && new SimpleType(SimpleType.SimpleClassifier.INTEGER, true)
                            .Coerce(operandType))
                        {
                            return;
                        }

                        if (Numeric(operandType))
                        {
                            valid = true;
                            break;
                        }
                    }
                    break;
                case "*":               
                case "/":
                case "~/":
                case "%":
                    if (Numeric(operandType))
                    {
                        valid = true;
                        break;
                    }
                    break;
                case "~^":
                    {
                        if (Numeric(rootType) || rootType is PointerType pt && !pt.IsDynamicPointer)
                            valid = new SimpleType(SimpleType.SimpleClassifier.INTEGER, true).Coerce(operandType);
                    }
                    break;
                case ">>":
                case "<<":
                    {
                        if (new SimpleType(SimpleType.SimpleClassifier.LONG).Coerce(rootType) && 
                            new SimpleType(SimpleType.SimpleClassifier.INTEGER, true).Coerce(operandType))
                            return;
                    }
                    break;
                case "==":
                case "!=":
                    {
                        if (_isComparableType(rootType) && _isComparableType(operandType))
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
                    {
                        if (rootType is SimpleType simpleRoot && operandType is SimpleType simpleOperand)
                        {
                            if (simpleRoot.Type == SimpleType.SimpleClassifier.BOOL && simpleOperand.Type == SimpleType.SimpleClassifier.BOOL)
                                return;    
                        }
                    }
                    break;
                case "&":
                case "|":
                case "^":
                    {
                        if (rootType is SimpleType && operandType is SimpleType)
                            valid = true;
                    }
                    break;
            }

            if (valid)
            {
                if (rootType.Classify() == TypeClassifier.NONE)
                    throw new SemanticException("Unable to apply operator to root type of none", position);

                if (!rootType.Coerce(operandType))
                {
                    if (operandType.Coerce(rootType))
                        rootType = operandType;
                    else
                        throw new SemanticException($"Unable to apply `{op}` to types of {rootType.ToString()} and {operandType.ToString()}", 
                            position);
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
                throw new SemanticException($"Unable to apply `{op}` to types of {rootType.ToString()} and {operandType.ToString()}",
                            position);
        }

        static private bool _large(SimpleType rootType)
        {
            if (new[] { SimpleType.SimpleClassifier.LONG, SimpleType.SimpleClassifier.DOUBLE }.Contains(rootType.Type))
                return true;

            return false;
        }
    }
}
