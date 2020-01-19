using System;
using System.Collections.Generic;
using System.Linq;

using Whirlwind.Syntax;
using Whirlwind.Types;
using Whirlwind.Semantic.Constexpr;

using static Whirlwind.Semantic.Checker.Checker;

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

        public DataType _generateType(ASTNode node, ValueCategory category = ValueCategory.RValue)
        {
            DataType dt = new NoneType();
            bool constant = false;

            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "TOKEN")
                {
                    var tokenNode = ((TokenNode)subNode);

                    switch (tokenNode.Tok.Type)
                    {
                        case "CONST":
                            constant = true;
                            break;
                        case "IDENTIFIER":
                            if (_table.Lookup(tokenNode.Tok.Value, _allowInternalTypes, out Symbol symbol))
                                dt = _getIdentifierType(symbol.DataType, tokenNode.Position);
                            // get proper error for inappropriate internal symbol
                            else if (_allowInternalTypes && _table.Lookup(tokenNode.Tok.Value, out Symbol _))
                            {
                                throw new SemanticException($"Internal symbol `{tokenNode.Tok.Value}` used in export context",
                                        tokenNode.Position);
                            }
                            else
                                throw new SemanticException($"Undefined symbol: `{tokenNode.Tok.Value}`", tokenNode.Position);
                            break;
                    }
                }
                else if (subNode.Name == "generic_spec")
                {
                    if (dt is GenericSelfType gst)
                    {
                        if (!gst.GetInstance(_generateTypeList((ASTNode)((ASTNode)subNode).Content[1]), out dt))
                            throw new SemanticException("Invalid generic specifier for " + dt.ToString(), subNode.Position);
                    }
                    else if (dt.Classify() != TypeClassifier.GENERIC)
                        throw new SemanticException("Unable to apply generic specifier to type of " + dt.ToString(), subNode.Position);
                    else
                        dt = _generateGeneric((GenericType)dt, (ASTNode)subNode);
                }
                else if (subNode.Name == "static_get")
                {
                    var idNode = (TokenNode)((ASTNode)subNode).Content[1];

                    if (dt is PackageType pkg)
                    {
                        if (pkg.Lookup(idNode.Tok.Value, out Symbol symbol))
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
                                throw new SemanticException($"Type class has no member named `{idNode.Tok.Value}`", idNode.Position);
                        }                          
                        else
                            throw new SemanticException("Unable to use type class value as type", subNode.Position);
                    }                      
                    else
                        throw new SemanticException("Unable to get static value from type of " + dt.ToString(), subNode.Position);
                }
                else
                    dt = _generateBaseType((ASTNode)subNode);
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

            if (new[] { TypeClassifier.SELF, TypeClassifier.GENERIC_SELF, TypeClassifier.GENERIC_SELF_INSTANCE }
                    .Contains(dt.Classify()) && _selfNeedsPointer)
                throw new SemanticException("Unable to declare incomplete type", node.Position);

            if (constant)
                dt.Constant = true;

            if (dt is PackageType)
                throw new SemanticException("Unable to package as data type", node.Position);

            dt.Category = category;
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

                    if (tok.Type == "ANY_TYPE")
                        return new AnyType();

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
                        case "SHORT_TYPE":
                            dt = SimpleType.SimpleClassifier.SHORT;
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

                    // okay because strings and floats are never unsigned
                    return new SimpleType(dt, tok.Value.StartsWith("u") || new[] { "byte", "char", "str" }.Contains(tok.Value));
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

                                    if (!Evaluator.TryEval(_nodes.Last()))
                                    {
                                        throw new SemanticException("Unable to initialize array with non constexpr array bound", component.Position);
                                    }

                                    var val = Evaluator.Evaluate(_nodes.Last());

                                    if (!new SimpleType(SimpleType.SimpleClassifier.INTEGER, true).Coerce(val.Type))
                                    {
                                        throw new SemanticException("Invalid data type for array bound", component.Position);
                                    }

                                    size = Int32.Parse(((ValueNode)val).Value);
                                    _nodes.RemoveLast();

                                    if (size == 0)
                                        throw new SemanticException("Unable to declare an array with an explicit length of 0", component.Position);
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
                else if (subNode.Name == "func_type")
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

                                        bool owned = arg.Content.First() is TokenNode,
                                            optional = arg.Content.Last() is TokenNode;

                                        var type = _generateType((ASTNode)arg.Content[Convert.ToInt32(owned)]);

                                        if (owned && !(type is PointerType pt && pt.IsDynamicPointer))
                                            throw new SemanticException("Own modifier must be used on a dynamic pointer", arg.Content[0].Position);

                                        args.Add(new Parameter("$arg" + args.Count.ToString(), 
                                            type, optional, false, false, owned));
                                    }
                                    else if (elem.Name == "func_arg_indef")
                                    {
                                        ASTNode arg = (ASTNode)elem;
                                        DataType dt = arg.Content.Count == 2 ? _generateType((ASTNode)arg.Content[1]) : new NoneType();

                                        args.Add(new Parameter("$arg" + args.Count.ToString(), dt, false, true, false, false));
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
                            returnType = new NoneType();
                            break;
                        case 1:
                            returnType = returnTypes[0];
                            break;
                        default:
                            returnType = new TupleType(returnTypes);
                            break;
                    }

                    return new FunctionType(args, returnType, async, isBoxed: true);
                }
                // assume pointer type
                else
                {
                    bool dynamicPointer = false;

                    if (((ASTNode)subNode).Content.Count == 3)
                        dynamicPointer = true;

                    // preserve self pointer context
                    var selfNeedsPtr = _selfNeedsPointer;
                    _selfNeedsPointer = false;

                    var baseDt = _generateType((ASTNode)((ASTNode)subNode).Content.Last());

                    _selfNeedsPointer = selfNeedsPtr;

                    return new PointerType(baseDt, dynamicPointer);
                }
            }

            // cover all your bases ;)
            return new NoneType();
        }

        private DataType _getIdentifierType(DataType sdt, TextPosition symbolPos)
        {
            if (sdt.Classify() == TypeClassifier.GENERIC_ALIAS)
                return ((GenericAlias)sdt).ReplacementType;
            // identifier checking not needed for self types because self types are compiled declared
            else if (!new[] { TypeClassifier.GENERIC_PLACEHOLDER, TypeClassifier.STRUCT,
                                    TypeClassifier.INTERFACE,  TypeClassifier.GENERIC, TypeClassifier.TYPE_CLASS,
                                    TypeClassifier.SELF, TypeClassifier.GENERIC_SELF }
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
