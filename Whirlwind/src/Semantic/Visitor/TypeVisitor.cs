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
                                if (symbol.DataType.Classify() == "TEMPLATE_ALIAS")
                                {
                                    dt = ((TemplateAlias)symbol.DataType).ReplacementType;
                                }
                                if (!new[] { "MODULE", "INTERFACE", "STRUCT" }.Contains(symbol.DataType.Classify()))
                                    throw new SemanticException("Identifier data type must be a module or an interface", tokenNode.Position);
                                
                                switch (symbol.DataType.Classify())
                                {
                                    case "MODULE":
                                        dt = ((ModuleType)symbol.DataType).GetInstance();
                                        break;
                                    case "STRUCT":
                                        var structType = ((StructType)symbol.DataType);
                                        structType.Instantiate();

                                        dt = structType;
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
                    if (dt.Classify() != "TEMPLATE")
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
            else if (dt.Classify() == "SIMPLE_TYPE" && ((SimpleType)dt).Type == SimpleType.DataType.VOID)
                throw new SemanticException("Incomplete type", node.Position);

            return dt;
        }

        private IDataType _generateBaseType(ASTNode node)
        {
            bool unsigned = false;

            foreach (var subNode in node.Content)
            {
                // only one token node
                if (subNode.Name == "TOKEN")
                    unsigned = true;
                else if (subNode.Name == "simple_types")
                {
                    SimpleType.DataType dt;
                    switch (((TokenNode)((ASTNode)subNode).Content[0]).Tok.Type)
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
                    if (!Numeric(new SimpleType(dt)) && unsigned)
                        throw new SemanticException("Invalid type for unsigned modifier", subNode.Position);
                    return new SimpleType(dt, unsigned);
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
                                case "MAP_TYPE":
                                    collectionType = "map";
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
                        case "map":
                            if (!Hashable(subTypes[0]))
                            {
                                throw new SemanticException("Unable to create map with an unhashable type", subNode.Position);
                            }
                            return new MapType(subTypes[0], subTypes[1]);
                    }
                }
                // assume function type
                else
                {
                    bool async = false;

                    var args = new List<Parameter>();
                    var returnTypes = new List<IDataType>();

                    foreach (var item in ((ASTNode)subNode).Content)
                    {
                        switch (item.Name)
                        {
                            case "TOKEN":
                                if (((TokenNode)item).Tok.Type == "ASYNC")
                                    async = true;
                                break;
                            case "args_decl_list":
                                args = _generateArgsDecl((ASTNode)item);
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
