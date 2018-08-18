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
                    case "heap_alloc":
                        _visitHeapAlloc((ASTNode)subNode);
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

            bool needsNew = new[] { "CallConstructor", "InitList", "HeapAllocType" }.Contains(_nodes.Last().Name);

            if (needsNew && !hasNew)
                throw new SemanticException("Missing `new` keyword to properly create instance.", node.Position);
            else if (!needsNew && hasNew)
            {
                if (new SimpleType(SimpleType.DataType.INTEGER, true).Coerce(_nodes.Last().Type))
                {
                    _nodes.Add(new ExprNode("HeapAlloc", new PointerType(new SimpleType(), 1)));
                    PushForward();
                }
                else
                    // new is first unless there is an await
                    throw new SemanticException("The `new` keyword is not valid for the given type", node.Content[hasAwait ? 1 : 0].Position);
            }

            if (_nodes.Last().Name == "CallAsync" && hasAwait)
            {
                _nodes.Add(new ExprNode("Await", ((Future)_nodes.Last().Type).Type));
                PushForward();
                return;
            }
            else if (hasAwait)
                // await always first element
                throw new SemanticException("The given expression is not awaitable", node.Content[0].Position);
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
                _nodes.Add(new ExprNode("MapComprehension", new MapType(elementType, valueType)));
            else
                _nodes.Add(new ExprNode("Comprehension", new ListType(elementType)));

            PushForward(sizeBack);
        }

        private void _visitTrailer(ASTNode node)
        {
            if (node.Content[0].Name == "template_spec")
            {
                if (_nodes.Last().Type.Classify() == "TEMPLATE")
                {
                    var templateType = _generateTemplate((TemplateType)_nodes.Last(), (ASTNode)node.Content[0]);

                    _nodes.Add(new ExprNode("CreateTemplate", templateType));
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
                    // make get member const correct
                    case ".":
                        string identifier = ((TokenNode)node.Content[1]).Tok.Value;
                        _nodes.Add(new ExprNode("GetMember", _getMember(root.Type, identifier, node.Content[0].Position, node.Content[1].Position)));
                        PushForward();
                        _nodes.Add(new IdentifierNode(identifier, new SimpleType(), false));
                        MergeBack();
                        break;
                    case "->":
                        if (root.Type.Classify() == "POINTER")
                        {
                            string pointerIdentifier = ((TokenNode)node.Content[1]).Tok.Value;
                            _nodes.Add(new ExprNode("GetMember", _getMember(((PointerType)root.Type).Type, pointerIdentifier, node.Content[0].Position, node.Content[1].Position)));
                            PushForward();
                            _nodes.Add(new IdentifierNode(pointerIdentifier, new SimpleType(), false));
                            MergeBack();
                            break;
                        }
                        else
                            throw new SemanticException("The '->' operator is not valid the given type", node.Content[0].Position);
                    case "[":
                        _visitSubscript(root.Type, node);
                        break;
                    case "{":
                        int initCount = 1;
                        var positions = new List<TextPosition>();
                        foreach (var item in ((ASTNode)node.Content[0]).Content)
                        {
                            if (item.Name == "TOKEN")
                            {
                                if (((TokenNode)item).Tok.Type == "IDENTIFIER")
                                {
                                    positions.Add(item.Position);
                                    _nodes.Add(new IdentifierNode(((TokenNode)item).Tok.Value, new SimpleType(), false));
                                }
                                    
                            }
                            else if (item.Name == "initializer")
                            {
                                _visitExpr((ASTNode)((ASTNode)item).Content[1]);
                                _nodes.Add(new ExprNode("Initializer", _nodes.Last().Type));
                                PushForward(2);
                                initCount++;
                            }
                        }
                        if (root.Type.Classify() == "MODULE")
                        {
                            var moduleInstance = ((ModuleType)root.Type).GetInstance();
                            for (int i = 1; i < initCount; i++)
                            {
                                var item = (ExprNode)_nodes[_nodes.Count - i];
                                if (moduleInstance.GetProperty(((ValueNode)item.Nodes[0]).Value, out Symbol sym))
                                {
                                    if (!sym.DataType.Coerce(item.Type))
                                        throw new SemanticException($"Unable to initializer property {sym.Name} with the given type", positions[i - 1]);
                                }
                                else
                                    throw new SemanticException($"Module has no public property {((ValueNode)item.Nodes[0]).Value}", positions[i - 1]);
                            }
                            _nodes.Add(new ExprNode("InitList", moduleInstance));
                            // add in root
                            PushForward(initCount + 1);
                            break;
                        }
                        else if (root.Type.Classify() == "STRUCT")
                        {
                            var members = ((StructType)root).Members;
                            if (initCount != members.Count)
                                throw new SemanticException("Struct initializer list must initialize all struct members", node.Content[0].Position);
                            for (int i = 1; i < initCount; i++)
                            {
                                var item = (ExprNode)_nodes[_nodes.Count - i];
                                string name = ((ValueNode)item.Nodes[0]).Value;
                                if (!members.ContainsKey(name))
                                    throw new SemanticException($"Struct has no member {name}", positions[i - 1]);
                                if (!members[name].Coerce(item.Nodes[1].Type))
                                    throw new SemanticException($"Unable to initialize member {name} with the given type", positions[i - 1]);
                            }
                            ((StructType)root.Type).Instantiate();
                            _nodes.Add(new ExprNode("InitList", root.Type));
                            // add in root
                            PushForward(initCount + 1);
                            break;
                        }
                        throw new SemanticException("Unable to apply initializer list to the given type", node.Position);
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
                                bool check1 = !CheckParameters((FunctionType)_nodes.Last().Type, new List<ParameterValue>() {
                                    new ParameterValue(iteratorType),
                                    new ParameterValue(iteratorType)
                                }).IsError;

                                var aggrFnReturnType = ((FunctionType)_nodes.Last().Type).ReturnType;

                                bool check2 = !CheckParameters((FunctionType)_nodes.Last().Type, new List<ParameterValue>() {
                                    new ParameterValue(aggrFnReturnType),
                                    new ParameterValue(iteratorType)
                                }).IsError;

                                if (check1 && check2)
                                {
                                    _nodes.Add(new ExprNode("FunctionAggregator", aggrFnReturnType));
                                    PushForward(2);
                                }
                                else
                                    throw new SemanticException("The given function is not valid as an aggregator for the given iterable", node.Content[1].Position);
                            }
                            else
                                throw new SemanticException("Aggregator expression must be a function", node.Content[1].Position);
                        }
                        // add in operator aggregator
                        else
                        {
                            string op = ((TokenNode)node.Content[2]).Tok.Type;

                            IDataType rootType = iteratorType;
                            // check first with default root type
                            CheckOperand(ref rootType, iteratorType, op, node.Content[2].Position);
                            // check with changed root type
                            if (rootType != iteratorType)
                                CheckOperand(ref rootType, iteratorType, op, node.Content[2].Position);

                            _nodes.Add(new ValueNode("Operator", new SimpleType(), op));
                            _nodes.Add(new ExprNode("OperatorAggregator", rootType));
                            PushForward(2);
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
            var args = node.Content.Count == 2 ? new List<ParameterValue>() : _generateArgsList((ASTNode)node.Content[1]);

            if (new[] { "MODULE", "FUNCTION" }.Contains(root.Type.Classify()))
            {
                bool isFunction = root.Type.Classify() == "FUNCTION";

                if (isFunction)
                {
                    var paramData = CheckParameters((FunctionType)root.Type, args);

                    if (paramData.IsError)
                        throw new SemanticException(paramData.ErrorMessage, paramData.ParameterPosition == -1 ? node.Position : 
                            ((ASTNode)node.Content[1]).Content.Where(x => x.Name == "decl_arg").ToArray()[paramData.ParameterPosition].Position
                        );
                }
                else if (!isFunction && !((ModuleType)root.Type).GetConstructor(args, out FunctionType constructor))
                    throw new SemanticException($"Module '{((ModuleType)root.Type).Name}' has no constructor the accepts the given parameters", node.Position);

                IDataType returnType = isFunction ? ((FunctionType)root.Type).ReturnType : ((ModuleType)root.Type).GetInstance();

                _nodes.Add(new ExprNode(isFunction ? (((FunctionType)root.Type).Async ? "CallAsync" : "Call") : "CallConstructor", returnType));

                if (args.Count == 0)
                    PushForward();
                else
                {
                    PushForward(args.Count);
                    // add function to beginning of call
                    ((ExprNode)_nodes[_nodes.Count - 1]).Nodes.Insert(0, _nodes[_nodes.Count - 2]);
                    _nodes.RemoveAt(_nodes.Count - 2);
                }
            }
            else if (root.Type.Classify() == "TEMPLATE")
            {
                if (((TemplateType)root.Type).Infer(args, out List<IDataType> inferredTypes))
                {
                    // always works - try auto eval if possible
                    ((TemplateType)root.Type).CreateTemplate(inferredTypes, out IDataType templateType);

                    _nodes.Add(new ExprNode("CreateTemplate", templateType));
                    PushForward();

                    _visitFunctionCall(node, root);
                }
                else
                    throw new SemanticException("Unable to infer type of template arguments", node.Position);
            }
            else
                throw new SemanticException("Unable to call non-callable type", node.Content[0].Position);
        }

        private void _visitSubscript(IDataType rootType, ASTNode node)
        {
            bool hasStartingExpr = false;
            int expressionCount = 0, colonCount = 0;
            var types = new List<IDataType>();
            var textPositions = new List<TextPosition>();

            foreach (var item in node.Content)
            {
                if (item.Name == "expr")
                {
                    _visitExpr((ASTNode)item);
                    hasStartingExpr = true;
                    expressionCount++;
                    types.Add(_nodes.Last().Type);
                    textPositions.Add(item.Position);
                }
                else if (item.Name == "slice")
                {
                    foreach (var subNode in ((ASTNode)item).Content)
                    {
                        if (subNode.Name == "expr")
                        {
                            _visitExpr((ASTNode)subNode);
                            expressionCount++;
                            types.Add(_nodes.Last().Type);
                            textPositions.Add(item.Position);
                        }
                        // only token is colon
                        else if (subNode.Name == "TOKEN")
                            colonCount++;
                    }
                }
            }

            string name;
            if (hasStartingExpr)
            {
                switch (expressionCount)
                {
                    case 1:
                        if (colonCount == 0)
                            name = "Subscript";
                        else
                            name = "SliceBegin";
                        break;
                    case 2:
                        if (colonCount == 1)
                            name = "Slice";
                        else
                            name = "SliceBeginStep";
                        break;
                    // 3
                    default:
                        name = "SliceStep";
                        break;
                }
            }
            else
            {
                switch (expressionCount)
                {
                    // no modification, just step or end
                    case 1:
                        name = colonCount == 1 ? "SliceEnd" : "SlicePureStep";
                        break;
                    // 2
                    default:
                        name = "SliceEndStep";
                        break;
                }
            }

            if (rootType.Classify() == "MAP")
            {
                if (name != "Subscript")
                    throw new SemanticException("Unable to perform slice on a map", node.Position);
                if (((MapType)rootType).KeyType.Coerce(_nodes.Last().Type))
                {
                    _nodes.Add(new ExprNode(name, ((MapType)rootType).ValueType));
                    PushForward(2);
                }
                else
                    throw new SemanticException("The subscript type and the key type must match", textPositions[0]);
            }
            else if (rootType.Classify() == "MODULE_INSTANCE")
            {
                var args = new List<ParameterValue>();

                switch (name)
                {
                    case "Subscript":
                    case "SliceBegin":
                        args.Add(new ParameterValue(types[0]));
                        break;
                    case "SliceEnd":
                        args.Add(new ParameterValue("end", types[0]));
                        break;
                    case "SliceEndStep":
                        args.Add(new ParameterValue("end", types[0]));
                        args.Add(new ParameterValue("step", types[1]));
                        break;
                    case "SliceBeginStep":
                        args.Add(new ParameterValue(types[0]));
                        args.Add(new ParameterValue("step", types[1]));
                        break;
                    case "SliceStep":
                        args.Add(new ParameterValue("step", types[0]));
                        break;
                    case "Slice":
                        args.AddRange(types.Select(x => new ParameterValue(x)));
                        break;
                }

                string methodName = name == "Subscript" ? "__subscript__" : "__slice__";
                if (HasOverload(rootType, methodName, args, out IDataType returnType))
                {
                    _nodes.Add(new ExprNode(name, returnType));
                    // capture root as well
                    PushForward(args.Count + 1);
                }
                else
                    throw new SemanticException("The given module defines an invalid overload for the `[]` operator", node.Position);
            }
            else
            {
                var intType = new SimpleType(SimpleType.DataType.INTEGER);
                if (!types.All(x => intType.Coerce(x))) {
                    throw new SemanticException($"Invalid index type for {(expressionCount == 1 && hasStartingExpr ? "subscript" : "slice")}", 
                        textPositions[Enumerable.Range(0, expressionCount).Where(x => !intType.Coerce(types[x])).First()]
                        );
                }

                IDataType elementType;
                switch (rootType.Classify())
                {
                    case "ARRAY":
                        elementType = ((ArrayType)rootType).ElementType;
                        break;
                    case "LIST":
                        elementType = ((ListType)rootType).ElementType;
                        break;
                    default:
                        throw new SemanticException($"Unable to perform  {(expressionCount == 1 && hasStartingExpr ? "subscript" : "slice")} on the given type", 
                            node.Position);
                }

                _nodes.Add(new ExprNode(name, name == "Subscript" ? elementType : rootType));
                // capture root as well
                PushForward(expressionCount + 1);
            }
        }

        private void _visitHeapAlloc(ASTNode node)
        {
            // new ( alloc_body ) -> types , expr
            var allocBody = (ASTNode)node.Content[2];

            IDataType dt = new SimpleType();
            bool hasSizeExpr = false;

            foreach (var item in allocBody.Content)
            {
                if (item.Name == "types")
                {
                    dt = _generateType((ASTNode)item);
                    _nodes.Add(new ValueNode("DataType", dt));
                }
                else if (item.Name == "expr")
                {
                    _visitExpr((ASTNode)item);
                    hasSizeExpr = true;
                }
                    
            }

            if (new[] { "ARRAY_TYPE", "LIST_TYPE", "MAP_TYPE", "FUNCTION" }.Contains(dt.Classify()))
                throw new SemanticException("Invalid data type for raw heap allocation", allocBody.Content[0].Position);

            if (!hasSizeExpr)
                _nodes.Add(new ValueNode("Literal", new SimpleType(SimpleType.DataType.INTEGER, true), "1"));
            else if (!new SimpleType(SimpleType.DataType.INTEGER, true).Coerce(_nodes.Last().Type))
                throw new SemanticException("Size of heap allocated type must be an integer", allocBody.Content[2].Position);

            _nodes.Add(new ExprNode("HeapAllocType", new PointerType(dt, 1)));
            PushForward(2);
        }
    }
}
