using Whirlwind.Parser;
using Whirlwind.Types;

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
                    case "IDENTIFIER":
                        if (_table.Lookup(((TokenNode)node.Content[0]).Tok.Value, out Symbol sym))
                        {
                            _nodes.Add(new ValueNode("Identifier", sym.DataType, sym.Name));
                            MergeBack();
                            return;
                        }
                        {
                            var token = ((TokenNode)node.Content[0]).Tok;
                            throw new SemanticException("Undefined Identifier", token.Index, token.Value.Length);
                        }
  
                    case "THIS":
                        if (_table.Lookup("$THIS", out Symbol instance))
                        {
                            _nodes.Add(new ValueNode("This", instance.DataType));
                            MergeBack();
                            return;
                        }
                        {
                            var token = ((TokenNode)node.Content[0]).Tok;
                            throw new SemanticException("Use of \'this\' outside of property", token.Index, token.Value.Length);
                        }
                }

                _nodes.Add(new ValueNode("Literal", new SimpleType(dt), ((TokenNode)node.Content[0]).Tok.Value));
                MergeBack();
            }
            else
            {
                switch (node.Content[0].Name())
                {
                    case "array":
                        var arr = _processSet((ASTNode)node.Content[0]);
                        _nodes.Add(new TreeNode("Array", new ArrayType(arr.Item1, arr.Item2)));
                        PushForward(arr.Item2);
                        break;
                    case "list":
                        var list = _processSet((ASTNode)node.Content[0]);
                        _nodes.Add(new TreeNode("List", new ListType(list.Item1)));
                        PushForward(list.Item2);
                        break;
                    case "map":
                        break;
                    case "inline_function":
                        break;
                    case "atom_types":
                        break;
                }
            }
        }

        private Tuple<IDataType, int> _processSet(ASTNode node)
        {
            IDataType dataType = new SimpleType(SimpleType.DataType.NULL);
            int size = 0;
            foreach (var element in node.Content)
            {
                if (element.Name() == "expr")
                {
                    _visitExpr((ASTNode)element);
                    if (!dataType.Coerce(_nodes.Last().Type()))
                    {
                        if (_nodes.Last().Type().Coerce(dataType))
                            dataType = _nodes.Last().Type();
                        else if (dataType.Classify() == "UNION")
                            ((UnionType)dataType).ValidTypes.Add(_nodes.Last().Type());
                        else
                            dataType = new UnionType(new List<IDataType>() { dataType, _nodes.Last().Type() });
                    }
                    size++;
                }     
            }
            return new Tuple<IDataType, int>(dataType, size);
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
    }
}
