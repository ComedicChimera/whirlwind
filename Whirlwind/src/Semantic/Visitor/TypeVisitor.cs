using Whirlwind.Parser;
using Whirlwind.Types;
using Whirlwind.Semantic.Constexpr;
using static Whirlwind.Semantic.Checker.Checker;

using System;
using System.Collections.Generic;
using System.Linq;


namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        public List<IDataType> _generateTypeList(ASTNode node)
        {
            var dataTypes = new List<IDataType>();
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "types")
                    dataTypes.Add(_generateType((ASTNode)subNode));
            }
            return dataTypes;
        }

        public IDataType _generateType(ASTNode node)
        {
            IDataType dt = new SimpleType();
            int pointers = 0;

            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "TOKEN")
                {
                    var tokenNode = ((TokenNode)subNode);

                    switch (tokenNode.Tok.Type)
                    {
                        case "*":
                            pointers++;
                            break;
                        case "IDENTIFIER":
                            if (_table.Lookup(tokenNode.Tok.Value, out Symbol symbol))
                            {
                                if (symbol.DataType.Classify() == TypeClassifier.TEMPLATE_ALIAS)
                                {
                                    dt = ((TemplateAlias)symbol.DataType).ReplacementType;
                                }
                                if (!new[] { TypeClassifier.OBJECT, TypeClassifier.STRUCT, TypeClassifier.INTERFACE }.Contains(symbol.DataType.Classify()))
                                    throw new SemanticException("Identifier data type must be a obj or an interface", tokenNode.Position);
                                
                                switch (symbol.DataType.Classify())
                                {
                                    case TypeClassifier.OBJECT:
                                        dt = ((ObjectType)symbol.DataType).GetInstance();
                                        break;
                                    case TypeClassifier.STRUCT:
                                        dt = ((StructType)symbol.DataType).GetInstance();
                                        break;
                                    case TypeClassifier.ENUM:
                                        dt = ((EnumType)symbol.DataType).GetInstance();
                                        break;
                                    // interface
                                    default:
                                        dt = symbol.DataType;
                                        break;
                                }
                            }
                            else
                            {
                                throw new SemanticException($"Undefined Identifier: '{tokenNode.Tok.Value}'", tokenNode.Position);
                            }
                            break;
                    }
                }
                else if (subNode.Name == "template_spec")
                {
                    if (dt.Classify() != TypeClassifier.TEMPLATE)
                        throw new SemanticException("Unable to apply template specifier to non-template type", subNode.Position);
                    dt = _generateTemplate((TemplateType)dt, (ASTNode)subNode);
                }
                else
                {
                    dt = _generateBaseType((ASTNode)subNode);
                }
            }

            if (pointers != 0)
            {
                dt = new PointerType(dt, pointers);
            }
            else if (dt.Classify() == TypeClassifier.SIMPLE && ((SimpleType)dt).Type == SimpleType.DataType.VOID)
                throw new SemanticException("Incomplete type", node.Position);

            return dt;
        }

        private IDataType _generateBaseType(ASTNode node)
        {
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "simple_types")
                {
                    SimpleType.DataType dt;
                    Token tok = ((TokenNode)((ASTNode)subNode).Content[0]).Tok;
                    switch (tok.Type)
                    {
                        case "BOOL_TYPE":
                            dt = SimpleType.DataType.BOOL;
                            break;
                        case "INT_TYPE":
                            dt = SimpleType.DataType.INTEGER;
                            break;
                        case "FLOAT_TYPE":
                            dt = SimpleType.DataType.FLOAT;
                            break;
                        case "DOUBLE_TYPE":
                            dt = SimpleType.DataType.DOUBLE;
                            break;
                        case "LONG_TYPE":
                            dt = SimpleType.DataType.LONG;
                            break;
                        case "BYTE_TYPE":
                            dt = SimpleType.DataType.BYTE;
                            break;
                        case "STRING_TYPE":
                            dt = SimpleType.DataType.STRING;
                            break;
                        case "CHAR_TYPE":
                            dt = SimpleType.DataType.CHAR;
                            break;
                        // void type
                        default:
                            dt = SimpleType.DataType.VOID;
                            break;
                    }

                    return new SimpleType(dt, tok.Value.StartsWith("u"));
                }
                else if (subNode.Name == "collection_types")
                {
                    string collectionType = "";
                    int size = 0;
                    var subTypes = new List<IDataType>();

                    foreach (var component in ((ASTNode)subNode).Content)
                    {
                        if (component.Name == "TOKEN")
                        {
                            switch (((TokenNode)component).Tok.Type)
                            {
                                case "ARRAY_TYPE":
                                    collectionType = "array";
                                    break;
                                case "LIST_TYPE":
                                    collectionType = "list";
                                    break;
                                case "DICT_TYPE":
                                    collectionType = "dict";
                                    break;
                            }
                        }
                        else if (component.Name == "expr" && collectionType == "array")
                        {
                            _visitExpr((ASTNode)component);
                            if (!Evaluator.TryEval((ExprNode)_nodes.Last()))
                            {
                                throw new SemanticException("Unable to initialize array with non constexpr array bound", component.Position);
                            }

                            var val = Evaluator.Evaluate((ExprNode)_nodes.Last());

                            if (val.Type != new SimpleType(SimpleType.DataType.INTEGER))
                            {
                                throw new SemanticException("Invalid data type for array bound", component.Position);
                            }

                            size = Int32.Parse(val.Value);
                        }
                        else if (component.Name == "types")
                        {
                            subTypes.Add(_generateType((ASTNode)component));
                        }
                    }

                    switch (collectionType)
                    {
                        case "array":
                            return new ArrayType(subTypes[0], size);
                        case "list":
                            return new ListType(subTypes[0]);
                        case "dict":
                            if (!Hashable(subTypes[0]))
                            {
                                throw new SemanticException("Unable to create dictionary with an unhashable type", subNode.Position);
                            }
                            return new DictType(subTypes[0], subTypes[1]);
                    }
                }
                else if (subNode.Name == "tuple_type")
                    return new TupleType(_generateTypeList((ASTNode)((ASTNode)subNode).Content[1]));
                // assume function type
                else
                {
                    bool async = false;

                    List<IDataType> args = new List<IDataType>(), 
                        returnTypes = new List<IDataType>();

                    bool collectingArgs = false;

                    foreach (var item in ((ASTNode)subNode).Content)
                    {
                        switch (item.Name)
                        {
                            case "TOKEN":
                                if (((TokenNode)item).Tok.Type == "ASYNC")
                                    async = true;
                                if (((TokenNode)item).Tok.Type == ")")
                                    collectingArgs = true;
                                break;
                            case "type_list":
                                if (collectingArgs)
                                    args = _generateTypeList((ASTNode)item);
                                else
                                    returnTypes = _generateTypeList((ASTNode)item);
                                break;
                        }
                    }

                    IDataType returnType;

                    switch (returnTypes.Count)
                    {
                        case 0:
                            returnType = new SimpleType();
                            break;
                        case 1:
                            returnType = returnTypes[0];
                            break;
                        default:
                            returnType = new TupleType(returnTypes);
                            break;
                    }

                    int p = 0;
                    return new FunctionType(args.Select(x => new Parameter("p" + (p++).ToString(), x, false, false)).ToList(), 
                        returnType, async);
                }
            }

            // cover all your bases ;)
            return new SimpleType();
        }
    }
}
