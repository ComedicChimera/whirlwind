using Whirlwind.Parser;
using Whirlwind.Types;
using Whirlwind.Generator.Constexpr;

using System;
using System.Linq;
using System.Collections.Generic;

namespace Whirlwind.Generator.Visitor
{
    partial class Visitor
    {
        private void _visitBase(ASTNode node)
        {
            if (node.Content[0].Name() == "TOKEN")
            {
                SimpleType.DataType dt = SimpleType.DataType.NULL; // default to null
                switch (((TokenNode)node.Content[0]).Tok.Type)
                {
                    case "VALUE":
                        dt = SimpleType.DataType.VALUE;
                        _nodes.Add(new ValueNode("Value", new SimpleType(dt)));
                        MergeBack();
                        return;
                    case "INTEGER_LITERAL":
                        dt = SimpleType.DataType.INTEGER;
                        break;
                    case "FLOAT_LITERAL":
                        dt = SimpleType.DataType.FLOAT;
                        break;
                    case "BOOL_LITERAL":
                        dt = SimpleType.DataType.BOOL;
                        break;
                    case "STRING_LITERAL":
                        dt = SimpleType.DataType.STRING;
                        break;
                    case "CHAR_LITERAL":
                        dt = SimpleType.DataType.CHAR;
                        break;
                    case "HEX_LITERAL":
                    case "BINARY_LITERAL":
                        _generateByteLiteral(((TokenNode)node.Content[0]).Tok);
                        MergeBack();
                        return;
                    case "RANGE_LITERAL":
                        _nodes.Add(new ValueNode(
                            "Range",
                            new ListType(new SimpleType(SimpleType.DataType.INTEGER)),
                            ((TokenNode)node.Content[0]).Tok.Value
                        ));
                        MergeBack();
                        return;
                    case "IDENTIFIER":
                        if (_table.Lookup(((TokenNode)node.Content[0]).Tok.Value, out Symbol sym))
                        {
                            _nodes.Add(new ValueNode("Identifier", sym.DataType, sym.Name));
                            MergeBack();
                            return;
                        }
                        {
                            throw new SemanticException("Undefined Identifier", node.Position);
                        }

                    case "THIS":
                        if (_table.Lookup("$THIS", out Symbol instance))
                        {
                            _nodes.Add(new ValueNode("This", instance.DataType));
                            MergeBack();
                            return;
                        }
                        {
                            throw new SemanticException("Use of \'this\' outside of property", node.Content[0].Position);
                        }
                }

                _nodes.Add(new ValueNode("Literal", new SimpleType(dt), ((TokenNode)node.Content[0]).Tok.Value));
            }
            else
            {
                switch (node.Content[0].Name())
                {
                    case "array":
                        var arr = _visitSet((ASTNode)node.Content[0]);
                        _nodes.Add(new TreeNode("Array", new ArrayType(arr.Item1, arr.Item2)));
                        if (arr.Item2 > 0)
                            PushForward(arr.Item2);
                        break;
                    case "list":
                        var list = _visitSet((ASTNode)node.Content[0]);
                        _nodes.Add(new TreeNode("List", new ListType(list.Item1)));
                        if (list.Item2 > 0)
                            PushForward(list.Item2);
                        break;
                    case "map":
                        var map = _visitMap((ASTNode)node.Content[0]);
                        _nodes.Add(new TreeNode("Map", new MapType(map.Item1, map.Item2)));
                        // will default to array if value is too small, so check not needed
                        PushForward(map.Item3);
                        break;
                    case "inline_function":
                        break;
                    case "atom_types":
                        var dt = _generateAtomType((ASTNode)node.Content[0]);
                        _nodes.Add(new TreeNode("DataTypeLiteral", new SimpleType(SimpleType.DataType.TYPE)));
                        _nodes.Add(new ValueNode("DataTypeValue", dt));
                        MergeBack();
                        break;
                }
            }
            MergeBack();
        }

        private Tuple<IDataType, int> _visitSet(ASTNode node)
        {
            IDataType elementType = new SimpleType(SimpleType.DataType.NULL);
            int size = 0;
            foreach (var element in node.Content)
            {
                if (element.Name() == "expr")
                {
                    _visitExpr((ASTNode)element);
                    _coerceSet(ref elementType, element.Position);
                    size++;
                }
            }
            return new Tuple<IDataType, int>(elementType, size);
        }

        private Tuple<IDataType, IDataType, int> _visitMap(ASTNode node)
        {
            IDataType keyType = new SimpleType(SimpleType.DataType.NULL), valueType = new SimpleType(SimpleType.DataType.NULL);
            bool isKey = true;
            int size = 0;

            foreach (var element in node.Content)
            {
                if (element.Name() == "expr")
                {
                    _visitExpr((ASTNode)element);
                    if (isKey)
                    {
                        _coerceSet(ref keyType, element.Position);

                        if (!TypeInterfaceChecker.Hashable(keyType))
                        {
                            throw new SemanticException("Unable to create map with unhashable type", element.Position);
                        }               
                        size++;
                    }
                    else
                    {
                        _coerceSet(ref valueType, element.Position);
                        // map pairs hold the key type
                        _nodes.Add(new TreeNode("MapPair", keyType));
                        // add 2 expr nodes to map pair
                        PushForward(2);
                    }
                    isKey = !isKey;
                }
            }

            return new Tuple<IDataType, IDataType, int>(keyType, valueType, size);
        }

        private void _coerceSet(ref IDataType baseType, TextPosition pos)
        {
            if (!baseType.Coerce(_nodes.Last().Type()))
            {
                if (_nodes.Last().Type().Coerce(baseType))
                    baseType = _nodes.Last().Type();
                else
                    throw new SemanticException("All values in a collection must be the same type", pos);
            }
        }

        private void _generateByteLiteral(Token token)
        {
            if (token.Type == "HEX_LITERAL")
            {
                if (token.Value.Length < 5 /* 5 to account for prefix */)
                {
                    _nodes.Add(new ValueNode("Literal", new SimpleType(SimpleType.DataType.BYTE), token.Value));
                }
                else
                {
                    string value = token.Value.Length % 2 == 0 ? token.Value : "0" + token.Value;
                    var pairs = Enumerable.Range(0, value.Length)
                        .Where(x => x % 2 == 0)
                        .Select(x => "0x" + value[x] + value[x + 1])
                        .Skip(1)
                        .Select(x => new ValueNode("Literal",
                            new SimpleType(SimpleType.DataType.BYTE),
                            x))
                        .ToArray();
                    _nodes.Add(new TreeNode("Array",
                        new ArrayType(new SimpleType(SimpleType.DataType.BYTE), pairs.Length)
                        ));
                    foreach (ValueNode node in pairs)
                    {
                        _nodes.Add(node);
                        MergeBack();
                    }
                    MergeBack();
                }
            }
            else
            {
                if (token.Value.Length < 11 /* 11 to account for prefix */)
                {
                    _nodes.Add(new ValueNode("Literal", new SimpleType(SimpleType.DataType.BYTE), token.Value));
                }
                else
                {
                    string value = token.Value.Length % 8 == 0 ? token.Value :
                        Enumerable.Repeat("0", 8 - token.Value.Length % 8) + token.Value;
                    var pairs = Enumerable.Range(0, value.Length)
                        .Where(x => x % 8 == 0)
                        .Select(x => "0x" + value.Substring(x, 8))
                        .Skip(1)
                        .Select(x => new ValueNode("Literal",
                            new SimpleType(SimpleType.DataType.BYTE),
                            x))
                        .ToArray();
                    _nodes.Add(new TreeNode("Array",
                        new ArrayType(new SimpleType(SimpleType.DataType.BYTE), pairs.Length)
                        ));
                    foreach (ValueNode node in pairs)
                    {
                        _nodes.Add(node);
                        MergeBack();
                    }
                    MergeBack();
                }
            }
        }

        private IDataType _generateAtomType(ASTNode node)
        {
            bool unsigned = false;

            foreach (var subNode in node.Content)
            {
                // only one token node
                if (subNode.Name() == "TOKEN")
                    unsigned = true;
                else if (subNode.Name() == "simple_types")
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
                        default:
                            dt = SimpleType.DataType.TYPE;
                            break;
                    }
                    return new SimpleType(dt, unsigned);
                }
                else if (subNode.Name() == "collection_types")
                {
                    string collectionType = "";
                    int size = 0;
                    var subTypes = new List<IDataType>();

                    foreach(var component in ((ASTNode)subNode).Content)
                    {
                        if (component.Name() == "TOKEN")
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
                        else if (component.Name() == "expr" && collectionType == "array")
                        {
                            _visitExpr((ASTNode)component);
                            if (!Evaluator.TryEval((TreeNode)_nodes.Last()))
                            {
                                throw new SemanticException("Unable to initialize array with non constexpr array bound", component.Position);
                            }

                            var val = Evaluator.Evaluate((TreeNode)_nodes.Last());

                            if (val.Type() != new SimpleType(SimpleType.DataType.INTEGER))
                            {
                                throw new SemanticException("Invalid data type for array bound", component.Position);
                            }

                            size = Int32.Parse(val.Value);
                        }
                        else if (component.Name() == "types")
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
                            if (!TypeInterfaceChecker.Hashable(subTypes[0]))
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
                        switch (item.Name()) {
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

                    return new FunctionType(args, returnTypes, async);
                }
            }

            // cover all your bases ;)
            return new SimpleType(SimpleType.DataType.NULL);
        }
    }
}
