using Whirlwind.Parser;
using Whirlwind.Types;

using System.Linq;

namespace Whirlwind.Generator.Visitor
{
    partial class Visitor
    {
        private void _visitBase(ASTNode node)
        {
            if (node.Content[0].Name() == "TOKEN")
            {
                SimpleType.DataType dt;
                switch (((TokenNode)node.Content[0]).Tok.Type)
                {
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
                    default:
                        return;
                }

                _nodes.Add(new ValueNode("Literal", new SimpleType(dt), ((TokenNode)node.Content[0]).Tok.Value));
                MergeBack();
            }
            else
            {
                switch (node.Content[0].Name())
                {

                }
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
    }
}
