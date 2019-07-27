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

        public List<DataType> _generateTypeList(ASTNode node)
        {
            var dataTypes = new List<DataType>();
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "types")
                    dataTypes.Add(_generateType((ASTNode)subNode));
            }
            return dataTypes;
        }

        public DataType _generateType(ASTNode node)
        {
            DataType dt = new VoidType();
            int pointers = 0;
            bool reference = false, owned = false, constant = false;

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
                        case "OWN":
                            owned = true;
                            break;
                        case "CONST":
                            constant = true;
                            break;
                        case "IDENTIFIER":
                            if (_table.Lookup(tokenNode.Tok.Value, out Symbol symbol))
                                dt = _getIdentifierType(symbol.DataType, tokenNode.Position);
                            else
                            {
                                throw new SemanticException($"Undefined Identifier: '{tokenNode.Tok.Value}'", tokenNode.Position);
                            }
                            break;
                    }
                }
                else if (subNode.Name == "generic_spec")
                {
                    if (dt.Classify() != TypeClassifier.GENERIC)
                        throw new SemanticException("Unable to apply generic specifier to non-generic type", subNode.Position);
                    dt = _generateGeneric((GenericType)dt, (ASTNode)subNode);
                }
                else if (subNode.Name == "static_get")
                {
                    var idNode = (TokenNode)((ASTNode)subNode).Content[1];

                    if (dt is Package pkg)
                    {
                        if (pkg.ExternalTable.Lookup(idNode.Tok.Value, out Symbol symbol))
                            dt = _getIdentifierType(symbol.DataType, idNode.Position);
                        else
                            throw new SemanticException($"The given package has no exported member `{idNode.Tok.Value}", idNode.Position);
                    }
                    else if (dt is CustomType ct)
                    {
                        if (_isTypeCast)
                        {
                            if (ct.GetInstanceByName(idNode.Tok.Value, out CustomNewType cnt))
                                dt = cnt;
                            else
                                throw new SemanticException($"Type class has no member by name `{idNode.Tok.Value}`", idNode.Position);
                        }                          
                        else
                            throw new SemanticException("Unable to use type class value as type", subNode.Position);
                    }
                        
                    else
                        throw new SemanticException("Unable to get static value from the given type", subNode.Position);
                }
                else
                {
                    dt = _generateBaseType((ASTNode)subNode);
                }
            }

            switch (dt.Classify())
            {
                case TypeClassifier.STRUCT:
                    dt = ((StructType)dt).GetInstance();
                    break;
                case TypeClassifier.INTERFACE:
                    dt = ((InterfaceType)dt).GetInstance();
                    break;
                case TypeClassifier.TYPE_CLASS:
                    dt = ((CustomType)dt).GetInstance();
                    break;
            }

            if (pointers != 0)
            {
                if (new[] { TypeClassifier.FUNCTION, TypeClassifier.FUNCTION_GROUP }.Contains(dt.Classify()))
                    throw new SemanticException("Unable to point to a function directly", node.Position);

                dt = new PointerType(dt, pointers, owned);
            }
            else if (_isVoid(dt) || dt.Classify() == TypeClassifier.SELF && _selfNeedsPointer)
                throw new SemanticException("Unable to declare incomplete type", node.Position);

            if (reference)
                dt = new ReferenceType(dt, owned);

            if (!reference && pointers == 0 && owned)
                throw new SemanticException("Cannot declare ownership over type that is not pointer or reference", node.Position);

            if (constant)
                dt.Constant = true;

            return dt;
        }

        private DataType _generateBaseType(ASTNode node)
        {
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "simple_types")
                {
                    SimpleType.SimpleClassifier dt;
                    Token tok = ((TokenNode)((ASTNode)subNode).Content[0]).Tok;

                    if (tok.Type == "VOID_TYPE")
                        return new VoidType();

                    switch (tok.Type)
                    {
                        case "BOOL_TYPE":
                            dt = SimpleType.SimpleClassifier.BOOL;
                            break;
                        case "INT_TYPE":
                            dt = SimpleType.SimpleClassifier.INTEGER;
                            break;
                        case "FLOAT_TYPE":
                            dt = SimpleType.SimpleClassifier.FLOAT;
                            break;
                        case "DOUBLE_TYPE":
                            dt = SimpleType.SimpleClassifier.DOUBLE;
                            break;
                        case "LONG_TYPE":
                            dt = SimpleType.SimpleClassifier.LONG;
                            break;
                        case "BYTE_TYPE":
                            dt = SimpleType.SimpleClassifier.BYTE;
                            break;
                        case "STRING_TYPE":
                            dt = SimpleType.SimpleClassifier.STRING;
                            break;
                        // char type
                        default:
                            dt = SimpleType.SimpleClassifier.CHAR;
                            break;
                    }

                    // okay because string is never unsigned
                    return new SimpleType(dt, tok.Value.StartsWith("u") && !tok.Value.StartsWith("s"));
                }
                else if (subNode.Name == "collection_types")
                {
                    string collectionType = "list"; // default to list if no other types overrode it
                    int size = -1;
                    var subTypes = new List<DataType>();

                    foreach (var component in ((ASTNode)subNode).Content)
                    {
                        switch (component.Name)
                        {
                            case "expr":
                                {
                                    _visitExpr((ASTNode)component);
                                    collectionType = "array";

                                    if (!Evaluator.TryEval((ExprNode)_nodes.Last()))
                                    {
                                        throw new SemanticException("Unable to initialize array with non constexpr array bound", component.Position);
                                    }

                                    var val = Evaluator.Evaluate((ExprNode)_nodes.Last());

                                    if (!new SimpleType(SimpleType.SimpleClassifier.INTEGER).Coerce(val.Type))
                                    {
                                        throw new SemanticException("Invalid data type for array bound", component.Position);
                                    }

                                    size = Int32.Parse(((ValueNode)val).Value);
                                    _nodes.RemoveAt(_nodes.Count - 1);
                                }
                                break;
                            case "TOKEN":
                                if (((TokenNode)component).Tok.Type == ":")
                                    collectionType = "dict";
                                if (((TokenNode)component).Tok.Type == "]" && subTypes.Count == 0)
                                    collectionType = "array";
                                break;
                            case "types":
                                subTypes.Add(_generateType((ASTNode)component));
                                break;
                            
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

                    List<DataType> returnTypes = new List<DataType>();
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
                                            arg.Content.Count == 2, false, false
                                            ));
                                    }
                                    else if (elem.Name == "func_arg_indef")
                                    {
                                        ASTNode arg = (ASTNode)elem;
                                        DataType dt = arg.Content.Count == 2 ? _generateType((ASTNode)arg.Content[1]) : new VoidType();

                                        args.Add(new Parameter("Arg" + args.Count.ToString(), dt, false, true, false));
                                    }
                                }
                                break;
                            case "type_list":
                                returnTypes = _generateTypeList((ASTNode)item);
                                break;
                        }
                    }

                    DataType returnType;

                    switch (returnTypes.Count)
                    {
                        case 0:
                            returnType = new VoidType();
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
            return new VoidType();
        }

        private DataType _getIdentifierType(DataType sdt, TextPosition symbolPos)
        {
            if (sdt.Classify() == TypeClassifier.GENERIC_ALIAS)
                return ((GenericAlias)sdt).ReplacementType;
            // identifier checking not needed for self types because self types are compiled declared
            else if (!new[] { TypeClassifier.GENERIC_PLACEHOLDER, TypeClassifier.STRUCT,
                                    TypeClassifier.INTERFACE,  TypeClassifier.GENERIC, TypeClassifier.TYPE_CLASS,
                                    TypeClassifier.SELF }
                .Contains(sdt.Classify()))
            {
                throw new SemanticException("Identifier data type must be a struct, type class, or interface",
                    symbolPos);
            }
            else
                return sdt;
        }
    }
}
