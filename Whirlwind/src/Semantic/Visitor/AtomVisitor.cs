using Whirlwind.Parser;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

using System.Linq;
using System.Collections.Generic;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitAtom(ASTNode node)
        {
            bool hasAwait = false, hasNew = false;
            foreach (INode subNode in node.Content)
            {
                switch (subNode.Name)
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
            IDataType elementType = new SimpleType();

            // used in case of map comprehension
            IDataType valueType = new SimpleType();

            int sizeBack = 0;
            bool isKeyPair = false;

            foreach (var item in node.Content)
            {
                if (item.Name == "expr")
                {
                    _visitExpr((ASTNode)item);

                    // second expr
                    if (sizeBack == 1)
                        elementType = _nodes.Last().Type;
                    else if (isKeyPair && sizeBack == 2)
                        valueType = _nodes.Last().Type;

                    sizeBack++;
                }
                else if (item.Name == "iterator")
                {
                    _table.AddScope();
                    _table.DescendScope();

                    _visitIterator((ASTNode)item);

                    sizeBack++;
                }
                else if (item.Name == "TOKEN")
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
            if (node.Content[0].Name == "template_spec")
            {
                if (_nodes.Last().Type.Classify() == "TEMPLATE")
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
                        _nodes.Add(new TreeNode("GetMember", _getMember(root.Type, identifier, node.Content[0].Position, node.Content[1].Position)));
                        PushForward();
                        _nodes.Add(new ValueNode("Identifier", new SimpleType(), identifier));
                        MergeBack();
                        break;
                    case "->":
                        if (root.Type.Classify() == "POINTER")
                        {
                            string pointerIdentifier = ((TokenNode)node.Content[1]).Tok.Value;
                            _nodes.Add(new TreeNode("GetMember", _getMember(((PointerType)root.Type).Type, pointerIdentifier, node.Content[0].Position, node.Content[1].Position)));
                            PushForward();
                            _nodes.Add(new ValueNode("Identifier", new SimpleType(), pointerIdentifier));
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
                        _visitFunctionCall(node, root);
                        break;
                    case "|>":
                        if (!Iterable(root.Type))
                            throw new SemanticException("Aggregated type must be iterable", node.Content[0].Position);

                        var iteratorType = _getIterableElementType(root.Type);

                        if (node.Content[1].Name == "expr")
                        {
                            _visitExpr((ASTNode)node.Content[1]);

                            if (_nodes.Last().Type.Classify() == "FUNCTION")
                            {
                                bool check1 = CheckParameters((FunctionType)_nodes.Last().Type, new List<ParameterValue>() {
                                    new ParameterValue(iteratorType),
                                    new ParameterValue(iteratorType)
                                });

                                var aggrFnReturnType = ((FunctionType)_nodes.Last().Type).ReturnType;

                                bool check2 = CheckParameters((FunctionType)_nodes.Last().Type, new List<ParameterValue>() {
                                    new ParameterValue(aggrFnReturnType),
                                    new ParameterValue(iteratorType)
                                });

                                if (check1 && check2)
                                {
                                    _nodes.Add(new TreeNode("FunctionAggregator", aggrFnReturnType));
                                    PushForward(2);
                                }
                                else
                                    throw new SemanticException("The given function is not valid as an aggregator for the given iterable", node.Content[1].Position);
                            }
                            else
                                throw new SemanticException("Aggregator expression must be a function", node.Content[1].Position);
                        }
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
                case "PACKAGE":
                    if (!((Package)type).ExternalTable.Lookup(name, out symbol))
                        throw new SemanticException($"Package has no member '{name}'", idPos);
                    break;
                default:
                    throw new SemanticException("The '.' operator is not valid on the given type", opPos);
            }

            return symbol.DataType;
        }

        private void _visitFunctionCall(ASTNode node, ITypeNode root)
        {
            var args = _generateArgsList((ASTNode)node.Content[0]);

            if (new[] { "MODULE", "FUNCTION" }.Contains(root.Type.Classify()))
            {
                bool isFunction = root.Type.Classify() == "FUNCTION";

                if (isFunction && !CheckParameters((FunctionType)root.Type, args))
                    throw new SemanticException("Invalid parameters for function call", node.Position);
                else if (!((ModuleType)root.Type).GetConstructor(args, out FunctionType constructor))
                    throw new SemanticException($"Module '{((ModuleType)root.Type).Name}' has no constructor the accepts the given parameters", node.Position);

                IDataType returnType = isFunction ? ((FunctionType)root.Type).ReturnType : ((ModuleType)root.Type).GetInstance();

                _nodes.Add(new TreeNode(isFunction ? "Call" : "CallConstructor", returnType));

                _generateFunctionCall((FunctionType)root.Type, args.Count);
            }
            else if (root.Type.Classify() == "TEMPLATE")
            {
                if (((TemplateType)root.Type).Infer(args, out List<IDataType> inferredTypes))
                {
                    // always works - try auto eval if possible
                    ((TemplateType)root.Type).CreateTemplate(inferredTypes, out IDataType templateType);

                    _nodes.Add(new TreeNode("CreateTemplate", templateType));
                    PushForward();

                    _visitFunctionCall(node, root);
                }
                else
                    throw new SemanticException("Unable to infer type of template arguments", node.Position);
            }
            else
                throw new SemanticException("Unable to call non-callable type", node.Content[0].Position);
        }

        private void _generateFunctionCall(FunctionType fn, int argCount)
        {
            _nodes.Add(new TreeNode("Call", fn.ReturnType));
            if (argCount == 0)
                PushForward();
            else
            {
                PushForward(argCount);
                // add function to beginning of call
                ((TreeNode)_nodes[_nodes.Count - 1]).Nodes.Insert(0, _nodes[_nodes.Count - 2]);
                _nodes.RemoveAt(_nodes.Count - 2);
            }
        }
    }
}
