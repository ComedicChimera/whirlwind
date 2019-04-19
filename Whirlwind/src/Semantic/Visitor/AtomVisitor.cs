using Whirlwind.Parser;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

using System;
using System.Linq;
using System.Collections.Generic;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        bool _isGetMode = false;

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
                    case "from_expr":
                        _visitFromExpr((ASTNode)subNode);
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

            bool needsNew = new[] { "CallConstructor", "InitList", "InitStruct" }.Contains(_nodes.Last().Name);

            if (needsNew && !hasNew)
                throw new SemanticException("Missing `new` keyword to properly create instance.", node.Position);
            else if (!needsNew && hasNew)
                throw new SemanticException("The `new` keyword is not valid for the given type", node.Content[hasAwait ? 1 : 0].Position);

            if (_nodes.Last().Name == "CallAsync" && hasAwait)
            {
                ((StructType)_nodes.Last().Type).GetInterface().GetFunction("result", out Symbol fn);

                _nodes.Add(new ExprNode("Await", ((FunctionType)fn.DataType).ReturnType));
                PushForward();
            }
            else if (hasAwait)
                // await always first element
                throw new SemanticException("The given expression is not awaitable", node.Content[0].Position);
        }

        private void _visitComprehension(ASTNode node)
        {
            DataType elementType = new SimpleType();

            // used in case of map comprehension
            DataType valueType = new SimpleType();

            int sizeBack = 0;
            bool isKeyPair = false, isCondition = false, isList = ((TokenNode)node.Content[0]).Tok.Type == "[";

            var body = new List<ASTNode>();

            foreach (var item in ((ASTNode)node.Content[1]).Content)
            {
                if (item.Name == "expr")
                {
                    if (isCondition)
                    {
                        _visitExpr((ASTNode)item);

                        if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                            throw new SemanticException("The condition of the comprehension must evaluate to a boolean", item.Position);

                        _nodes.Add(new ExprNode("Filter", new SimpleType(SimpleType.SimpleClassifier.BOOL)));
                        MergeBack();

                        sizeBack++;
                    }
                    else
                        body.Add((ASTNode)item);
                }
                else if (item.Name == "iterator")
                {
                    _table.AddScope();
                    _table.DescendScope();

                    _visitIterator((ASTNode)item);

                    sizeBack++;

                    foreach (var expr in body)
                    {
                        _visitExpr(expr);

                        // first expression
                        if (sizeBack == 1)
                            elementType = _nodes.Last().Type;
                        else if (isKeyPair && sizeBack == 2)
                            valueType = _nodes.Last().Type;

                        sizeBack++;
                    }
                }
                else if (item.Name == "TOKEN")
                {
                    if (((TokenNode)item).Tok.Type == ":")
                    {
                        if (isList)
                            throw new SemanticException("Unable to have key-value pairs in a list comprehension", item.Position);

                        isKeyPair = true;
                    }     
                    else if (((TokenNode)item).Tok.Type == "WHEN")
                        isCondition = true;
                }
            }

            _table.AscendScope();

            if (isKeyPair)
                _nodes.Add(new ExprNode("DictComprehension", new DictType(elementType, valueType)));
            else if (isList)
                _nodes.Add(new ExprNode("ListComprehension", new ListType(elementType)));
            else
                throw new SemanticException("Unable to create an array comprehension", node.Position);

            PushForward(sizeBack);
        }

        private void _visitTrailer(ASTNode node)
        {
            if (node.Content[0].Name == "template_spec")
            {
                if (_nodes.Last().Type.Classify() == TypeClassifier.TEMPLATE)
                {
                    var templateType = _generateTemplate((TemplateType)_nodes.Last().Type, (ASTNode)node.Content[0]);

                    _nodes.Add(new ExprNode("CreateTemplate", templateType));
                    PushForward();
                }
                else
                    throw new SemanticException("Unable to apply template specifier to non-template type", node.Position);
            }
            else if (node.Content[0].Name == "static_get")
            {
                Symbol symbol = _getStaticMember(_nodes.Last().Type, 
                    ((TokenNode)((ASTNode)node.Content[0]).Content[1]).Tok.Value, 
                    ((ASTNode)node.Content[0]).Content[0].Position,
                     ((ASTNode)node.Content[0]).Content[1].Position
                     );

                if (symbol.Modifiers.Contains(Modifier.CONSTEXPR))
                    _nodes.Add(new ConstexprNode(symbol.Name, symbol.DataType, symbol.Name));
                else
                    _nodes.Add(new IdentifierNode(symbol.Name, symbol.DataType, symbol.Modifiers.Contains(Modifier.CONSTANT)));
                _nodes.Add(new ExprNode("StaticGet", symbol.DataType));

                PushForward(2);
            }
            // all other first elements are tokens
            else
            {
                var root = _nodes.Last();

                switch (((TokenNode)node.Content[0]).Tok.Type)
                {
                    // make get member const correct
                    case ".":
                        {
                            var token = ((TokenNode)node.Content[1]).Tok;

                            if (token.Type == "IDENTIFIER")
                            {
                                string identifier = ((TokenNode)node.Content[1]).Tok.Value;

                                var symbol = _getMember(root.Type, identifier, node.Content[0].Position, node.Content[1].Position);
                                _nodes.Add(new ExprNode("GetMember", symbol.DataType));

                                PushForward();
                                _nodes.Add(new IdentifierNode(identifier, new SimpleType(), symbol.Modifiers.Contains(Modifier.CONSTANT)));
                                MergeBack();
                            }
                            else
                            {
                                if (root.Type.Classify() == TypeClassifier.TUPLE)
                                {
                                    int tupleNdx = Int32.Parse(token.Value);
                                    var dataTypes = ((TupleType)root.Type).Types;

                                    if (tupleNdx < dataTypes.Count)
                                    {
                                        _nodes.Add(new ValueNode("IntegerMember", new SimpleType(SimpleType.SimpleClassifier.INTEGER), token.Value));
                                        _nodes.Add(new ExprNode("GetTupleMember", dataTypes[tupleNdx]));

                                        PushForward(2);
                                    }
                                    else
                                        throw new SemanticException(tupleNdx + " is not a member of the given tuple", node.Content[1].Position);
                                }
                                else
                                    throw new SemanticException("Unable to use get integer member from non-tuple", node.Content[1].Position);
                            }
                        }
                        
                        break;
                    case "->":
                        if (root.Type.Classify() == TypeClassifier.POINTER)
                        {
                            string pointerIdentifier = ((TokenNode)node.Content[1]).Tok.Value;

                            var symbol = _getMember(((PointerType)root.Type).Type, pointerIdentifier, node.Content[0].Position, node.Content[1].Position);
                            _nodes.Add(new ExprNode("GetMember", symbol.DataType));

                            PushForward();
                            _nodes.Add(new IdentifierNode(pointerIdentifier, new SimpleType(), symbol.Modifiers.Contains(Modifier.CONSTANT)));
                            MergeBack();
                            break;
                        }
                        else
                            throw new SemanticException("The '->' operator is not valid the given type", node.Content[0].Position);
                    case "[":
                        _visitSubscript(root.Type, node);
                        break;
                    case "{":
                        int initCount = 0;
                        var positions = new List<TextPosition>();
                        foreach (var item in ((ASTNode)node.Content[1]).Content)
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

                        if (root.Type.Classify() == TypeClassifier.STRUCT)
                        {
                            var members = ((StructType)root.Type).Members;

                            if (initCount != members.Count)
                                throw new SemanticException("Struct initializer list must initialize all struct members", node.Content[0].Position);
                            for (int i = 1; i < initCount + 1; i++)
                            {
                                var item = (ExprNode)_nodes[_nodes.Count - i];
                                string name = ((IdentifierNode)item.Nodes[0]).IdName;
                                if (!members.ContainsKey(name))
                                    throw new SemanticException($"Struct has no member {name}", positions[i - 1]);
                                if (!members[name].DataType.Coerce(item.Nodes[1].Type))
                                    throw new SemanticException($"Unable to initialize member {name} with the given type", positions[i - 1]);
                            }
                            _nodes.Add(new ExprNode("InitList", ((StructType)root.Type).GetInstance()));
                            // add in root
                            PushForward(initCount + 1);
                            break;
                        }
                        throw new SemanticException("Unable to apply initializer list to the given type", node.Position);
                    case "(":
                        _visitFunctionCall(node, root);
                        break;
                }
            }
        }

        private Symbol _getMember(DataType type, string name, TextPosition opPos, TextPosition idPos)
        {
            Symbol symbol;
            switch (type.Classify())
            {
                case TypeClassifier.STRUCT_INSTANCE:
                    if (((StructType)type).Members.ContainsKey(name))
                        return new Symbol(name, ((StructType)type).Members[name].DataType);
                    goto default;
                case TypeClassifier.INTERFACE_INSTANCE:
                    if (!((InterfaceType)type).GetFunction(name, out symbol))
                        throw new SemanticException($"Interface has no function `{name}`", idPos);
                    break;
                default:
                    if (!type.GetInterface().GetFunction(name, out symbol))
                        throw new SemanticException($"Type has no interface member `{name}`", idPos);
                    break;
            }

            return symbol;
        }

        private Symbol _getStaticMember(DataType type, string name, TextPosition opPos, TextPosition idPos)
        {
            Symbol symbol;

            switch (type.Classify())
            {
                case TypeClassifier.PACKAGE:
                    if (!((Package)type).ExternalTable.Lookup(name, out symbol))
                        throw new SemanticException($"Package has no member `{name}`", idPos);
                    break;
                default:
                    throw new SemanticException("The `::` operator is not valid on the given type", opPos);
            }

            return symbol;
        }

        private void _visitFunctionCall(ASTNode node, ITypeNode root)
        {
            var args = node.Content.Count == 2 ? new ArgumentList() : _generateArgsList((ASTNode)node.Content[1]);

            if (new[] { TypeClassifier.STRUCT, TypeClassifier.FUNCTION }.Contains(root.Type.Classify()))
            {
                bool isFunction = root.Type.Classify() == TypeClassifier.FUNCTION;

                if (isFunction)
                {
                    var paramData = CheckArguments((FunctionType)root.Type, args);

                    if (paramData.IsError)
                        throw new SemanticException(paramData.ErrorMessage, paramData.ParameterPosition == -1 ? node.Position : 
                            ((ASTNode)node.Content[1]).Content.Where(x => x.Name == "arg").ToArray()[paramData.ParameterPosition].Position
                        );
                }
                else if (!isFunction && !((StructType)root.Type).GetConstructor(args, out FunctionType constructor))
                    throw new SemanticException($"Typeclass `{((StructType)root.Type).Name}` has no constructor the accepts the given parameters", node.Position);

                DataType returnType = isFunction ? ((FunctionType)root.Type).ReturnType : ((StructType)root.Type).GetInstance();

                _nodes.Add(new ExprNode(isFunction ? (((FunctionType)root.Type).Async ? "CallAsync" : "Call") : "CallConstructor", returnType));

                if (args.Count() == 0)
                    PushForward();
                else
                {
                    PushForward(args.Count());
                    // add function to beginning of call
                    ((ExprNode)_nodes[_nodes.Count - 1]).Nodes.Insert(0, _nodes[_nodes.Count - 2]);
                    _nodes.RemoveAt(_nodes.Count - 2);
                }
            }
            else if (root.Type.Classify() == TypeClassifier.TEMPLATE)
            {
                if (((TemplateType)root.Type).Infer(args, out List<DataType> inferredTypes))
                {
                    // always works - try auto eval if possible
                    ((TemplateType)root.Type).CreateTemplate(inferredTypes, out DataType templateType);

                    _nodes.Add(new ExprNode("CreateTemplate", templateType));
                    PushForward(args.Count() + 1);

                    _visitFunctionCall(node, _nodes.Last());
                }
                else
                    throw new SemanticException("Unable to infer type of template arguments", node.Position);
            }
            else if (root.Type.Classify() == TypeClassifier.STRUCT)
            {
                if (args.Count() > 0)
                    throw new SemanticException("Struct constructor cannot accept arguments", node.Position);

                _nodes.Add(new ExprNode("InitStruct", ((StructType)root.Type).GetInstance()));

                PushForward();
            }
            else if (root.Type.Classify() == TypeClassifier.FUNCTION_GROUP)
            {
                FunctionGroup fg = (FunctionGroup)root.Type;

                if (fg.GetFunction(args, out FunctionType ft))
                {
                    _nodes.Add(new ExprNode("CallFunctionOverload", ft.ReturnType));

                    PushForward(args.Count() + 1);
                }
                else
                    throw new SemanticException("No function in the function group matches the given arguments", node.Position);
            }
            else
                throw new SemanticException("Unable to call non-callable type", node.Content[0].Position);
        }

        private void _visitSubscript(DataType rootType, ASTNode node)
        {
            bool hasStartingExpr = false;
            int expressionCount = 0, colonCount = 0;
            var types = new List<DataType>();
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
                    // empty slice (idk why you would do this, but it is technically possible)
                    case 0:
                        name = "SliceCopy";
                        break;
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

            if (rootType.Classify() == TypeClassifier.DICT)
            {
                if (name != "Subscript")
                    throw new SemanticException("Unable to perform slice on a map", node.Position);
                if (((DictType)rootType).KeyType.Coerce(_nodes.Last().Type))
                {
                    _nodes.Add(new ExprNode(name, ((DictType)rootType).ValueType));
                    PushForward(2);
                }
                else
                    throw new SemanticException("The subscript type and the key type must match", textPositions[0]);
            }
            // fix operator overload
            /*else if (rootType.Classify() == TypeClassifier.OBJECT_INSTANCE)
            {
                var args = new List<DataType>();

                switch (name)
                {
                    case "Subscript":
                    case "SliceBegin":
                        args.Add(types[0]);
                        break;
                    case "SliceEnd":
                        args.AddRange(new List<DataType>() { new SimpleType(SimpleType.SimpleClassifier.INTEGER), types[0] });
                        break;
                    case "SliceEndStep":
                        args.AddRange(new List<DataType>() { new SimpleType(SimpleType.SimpleClassifier.INTEGER), types[0], types[1] });
                        break;
                    case "SliceBeginStep":
                        args.AddRange(new List<DataType>() { types[0], new SimpleType(SimpleType.SimpleClassifier.INTEGER), types[1] });
                        break;
                    case "SliceStep":
                        args.AddRange(new List<DataType>() {
                            new SimpleType(SimpleType.SimpleClassifier.INTEGER),
                            new SimpleType(SimpleType.SimpleClassifier.INTEGER),
                            types[0]
                        });
                        break;
                    case "Slice":
                        args.AddRange(types);
                        break;
                }

                string methodName = string.Format("__%s%s", _isGetMode ? "get" : "set", name == "Subscript" ? "item__" : "region__");
                if (HasOverload(rootType, methodName, new ArgumentList(args), out DataType returnType))
                {
                    _nodes.Add(new ExprNode(name, returnType));
                    // capture root as well
                    PushForward(args.Count + 1);
                }
                else
                    throw new SemanticException("The given obj defines an invalid overload for the `[]` operator", node.Position);
            }*/
            else
            {
                var intType = new SimpleType(SimpleType.SimpleClassifier.INTEGER);
                if (!types.All(x => intType.Coerce(x))) {
                    throw new SemanticException($"Invalid index type for {(expressionCount == 1 && hasStartingExpr ? "subscript" : "slice")}", 
                        textPositions[Enumerable.Range(0, expressionCount).Where(x => !intType.Coerce(types[x])).First()]
                        );
                }

                DataType elementType;
                switch (rootType.Classify())
                {
                    case TypeClassifier.ARRAY:
                        elementType = ((ArrayType)rootType).ElementType;
                        break;
                    case TypeClassifier.LIST:
                        elementType = ((ListType)rootType).ElementType;
                        break;
                    case TypeClassifier.SIMPLE:
                        if (((SimpleType)rootType).Type == SimpleType.SimpleClassifier.STRING)
                        {
                            elementType = new SimpleType(SimpleType.SimpleClassifier.CHAR);
                            break;
                        }

                        // yeah, yeah, i know
                        goto default;
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
            // make ( alloc_body ) -> types , expr
            var allocBody = (ASTNode)node.Content[2];

            DataType dt = new SimpleType();
            bool hasSizeExpr = false, isStructAlloc = false;

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
                else if (item.Name == "base")
                {
                    _visitBase((ASTNode)item);
                    hasSizeExpr = true;
                }
                else if (item.Name == "trailer")
                {
                    _visitTrailer((ASTNode)item);
                    hasSizeExpr = false;
                    isStructAlloc = true;

                    dt = _nodes.Last().Type;
                }
            }

            if (isStructAlloc)
            {
                if (dt.Classify() != TypeClassifier.STRUCT_INSTANCE)
                    throw new SemanticException("Invalid dynamic allocation call", node.Content.Last().Position);

                _nodes.Add(new ExprNode("HeapAllocStruct", new PointerType(dt, 1)));
                PushForward();
            }
            else
            {
                if (new[] { TypeClassifier.LIST, TypeClassifier.DICT, TypeClassifier.FUNCTION }.Contains(dt.Classify()))
                    throw new SemanticException("Invalid data type for raw heap allocation", allocBody.Content[0].Position);

                if (!hasSizeExpr)
                    _nodes.Add(new ValueNode("Literal", new SimpleType(SimpleType.SimpleClassifier.INTEGER, true), "1"));
                else if (!new SimpleType(SimpleType.SimpleClassifier.INTEGER, true).Coerce(_nodes.Last().Type))
                    throw new SemanticException("Size of heap allocated type must be an integer", allocBody.Content[2].Position);

                _nodes.Add(new ExprNode("HeapAllocType", new PointerType(dt, 1)));
                PushForward(2);
            }
        }

        private void _visitFromExpr(ASTNode node)
        {
            _visitExpr((ASTNode)node.Content[1]);

            if (_nodes.Last().Type is CustomNewType cnt)
            {
                DataType dt = cnt.Values.Count > 1 ? new TupleType(cnt.Values) : cnt.Values.First();

                _nodes.Add(new ExprNode("ExtractValue", dt));
                PushForward();
            }
            else
                throw new SemanticException("Unable to extract value from non-value-holding type class member", node.Content[1].Position);
        }
    }
}
