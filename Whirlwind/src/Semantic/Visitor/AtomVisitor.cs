using Whirlwind.Parser;
using Whirlwind.Types;

using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitAtom(ASTNode node)
        {
            bool hasAwait = false, hasNew = false;
            foreach (INode subNode in node.Content)
            {
                switch (subNode.Name())
                {
                    // base item
                    case "base":
                        _visitBase((ASTNode)subNode);
                        break;
                    case "comprehension":
                        _visitComprehension((ASTNode)subNode);
                        break;
                    case "trailer":
                        _visitTrailer((ASTNode)subNode);
                        break;
                    case "TOKEN":
                        switch (((TokenNode)subNode).Tok.Type) {
                            case "AWAIT":
                                hasAwait = true;
                                break;
                            case "NEW":
                                hasNew = true;
                                break;
                        }
                        break;
                }
            }
        }

        private void _visitComprehension(ASTNode node)
        {
            IDataType elementType = new SimpleType(SimpleType.DataType.NULL);

            // used in case of map comprehension
            IDataType valueType = new SimpleType(SimpleType.DataType.NULL);

            int sizeBack = 0;
            bool isKeyPair = false;

            foreach (var item in node.Content)
            {
                if (item.Name() == "expr")
                {
                    _visitExpr((ASTNode)item);

                    // second expr
                    if (sizeBack == 1)
                        elementType = _nodes.Last().Type();
                    else if (isKeyPair && sizeBack == 2)
                        valueType = _nodes.Last().Type();

                    sizeBack++;
                }
                else if (item.Name() == "iterator")
                {
                    _table.AddScope();
                    _table.DescendScope();

                    _visitIterator((ASTNode)item);

                    sizeBack++;
                }
                else if (item.Name() == "TOKEN")
                {
                    if (((TokenNode)item).Tok.Type == ":")
                        isKeyPair = true;
                }
            }

            _table.AscendScope();

            if (isKeyPair)
                _nodes.Add(new TreeNode("MapComprehension", new MapType(elementType, valueType)));
            else
                _nodes.Add(new TreeNode("Comprehension", new ListType(elementType)));

            PushForward(sizeBack);
        }

        private void _visitTrailer(ASTNode node)
        {
            if (node.Content[0].Name() == "template_spec")
            {
                if (_nodes.Last().Type().Classify() == "TEMPLATE")
                {
                    var templateType = _generateTemplate((TemplateType)_nodes.Last(), (ASTNode)node.Content[0]);

                    _nodes.Add(new TreeNode("CreateTemplate", templateType));
                    PushForward();
                }
                else
                    throw new SemanticException("Unable to apply template specifier to non-template type", node.Position);
            }
            // all other first elements are tokens
            else
            {
                var root = _nodes.Last();

                switch (((TokenNode)node.Content[0]).Tok.Type)
                {
                    case ".":
                        string identifier = ((TokenNode)node.Content[1]).Tok.Value;
                        _nodes.Add(new TreeNode("GetMember", _getMember(root.Type(), identifier, node.Content[0].Position, node.Content[1].Position)));
                        PushForward();
                        _nodes.Add(new ValueNode("Identifier", new SimpleType(SimpleType.DataType.NULL), identifier));
                        MergeBack();
                        break;
                    case "->":
                        if (root.Type().Classify() == "POINTER")
                        {
                            string pointerIdentifier = ((TokenNode)node.Content[1]).Tok.Value;
                            _nodes.Add(new TreeNode("GetMember", _getMember(((PointerType)root.Type()).Type, pointerIdentifier, node.Content[0].Position, node.Content[1].Position)));
                            PushForward();
                            _nodes.Add(new ValueNode("Identifier", new SimpleType(SimpleType.DataType.NULL), pointerIdentifier));
                            MergeBack();
                            break;
                        }
                        else
                            throw new SemanticException("The '->' operator is not valid the given type", node.Content[0].Position);
                    case "[":
                        break;
                    case "{":
                        break;
                    case "(":
                        break;
                }
            }
        }

        private IDataType _getMember(IDataType type, string name, TextPosition opPos, TextPosition idPos)
        {
            Symbol symbol;
            switch (type.Classify())
            {
                case "MODULE_INSTANCE":
                    if (!((ModuleInstance)type).GetProperty(name, out symbol))
                        throw new SemanticException($"Module instance has no property '{name}'", idPos);
                    break;
                case "MODULE":
                    if (!((ModuleType)type).GetMember(name, out symbol))
                        throw new SemanticException($"Module has no static member '{name}'", idPos);
                    break;
                case "STRUCT_INSTANCE":
                    if (((StructType)type).Members.ContainsKey(name))
                        return ((StructType)type).Members[name];
                    else
                        throw new SemanticException($"Struct has no member '{name}'", idPos);
                default:
                    throw new SemanticException("The '.' operator is not valid on the given type", opPos);
            }

            return symbol.DataType;
        }
    }
}
