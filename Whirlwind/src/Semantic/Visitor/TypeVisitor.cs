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
        bool _selfNeedsPointer = false;

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
            bool reference = false;

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
                        case "REF":
                            reference = true;
                            break;
                        case "IDENTIFIER":
                            if (_table.Lookup(tokenNode.Tok.Value, out Symbol symbol))
                            {
                                if (symbol.DataType.Classify() == TypeClassifier.TEMPLATE_ALIAS)
                                    dt = ((TemplateAlias)symbol.DataType).ReplacementType;
                                else if (!new[] { TypeClassifier.TEMPLATE_PLACEHOLDER, TypeClassifier.OBJECT, TypeClassifier.STRUCT,
                                    TypeClassifier.INTERFACE, TypeClassifier.ENUM, TypeClassifier.TEMPLATE }.Contains(symbol.DataType.Classify()))
                                {
                                    throw new SemanticException("Identifier data type must be a struct, type class, enum, or interface",
                                        tokenNode.Position);
                                }
                                else
                                    dt = symbol.DataType;
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

            switch (dt.Classify())
            {
                case TypeClassifier.OBJECT:
                    dt = ((ObjectType)dt).GetInstance();
                    break;
                case TypeClassifier.STRUCT:
                    dt = ((StructType)dt).GetInstance();
                    break;
                case TypeClassifier.ENUM:
                    dt = ((EnumType)dt).GetInstance();
                    break;
                case TypeClassifier.INTERFACE:
                    dt = ((InterfaceType)dt).GetInstance();
                    break;
            }

            if (pointers != 0)
            {
                dt = new PointerType(dt, pointers);
            }
            else if (_isVoid(dt) || dt.Classify() == TypeClassifier.SELF && _selfNeedsPointer)
                throw new SemanticException("Unable to declare incomplete type", node.Position);

            if (reference)
                dt = new ReferenceType(dt);

            

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

                    List<IDataType> returnTypes = new List<IDataType>();
                    List<Parameter> args = new List<Parameter>();

                    foreach (var item in ((ASTNode)subNode).Content)
                    {
                        switch (item.Name)
                        {
                            case "TOKEN":
                                if (((TokenNode)item).Tok.Type == "ASYNC")
                                    async = true;
                                break;
                            case "func_arg_types":
                                foreach (var elem in ((ASTNode)item).Content)
                                {
                                    if (elem.Name == "func_arg_type")
                                    {
                                        ASTNode arg = (ASTNode)elem;

                                        args.Add(new Parameter("Arg" + args.Count.ToString(), 
                                            _generateType((ASTNode)arg.Content[0]), 
                                            arg.Content.Count == 2
                                            ));
                                    }
                                    else if (elem.Name == "func_arg_indef")
                                    {
                                        ASTNode arg = (ASTNode)elem;
                                        IDataType dt = arg.Content.Count == 2 ? _generateType((ASTNode)arg.Content[1]) : new SimpleType();

                                        args.Add(new Parameter("Arg" + args.Count.ToString(), dt, true, false));
                                    }
                                }
                                break;
                            case "type_list":
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

                    return new FunctionType(args, returnType, async);
                }
            }

            // cover all your bases ;)
            return new SimpleType();
        }
    }
}
