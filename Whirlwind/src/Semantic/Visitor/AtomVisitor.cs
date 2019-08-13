using System;
using System.Collections.Generic;
using System.Linq;
using Whirlwind.Parser;
using Whirlwind.Types;
using static Whirlwind.Semantic.Checker.Checker;

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

        private void _visitTrailer(ASTNode node)
        {
            if (node.Content[0].Name == "generic_spec")
            {
                if (_nodes.Last().Type.Classify() == TypeClassifier.GENERIC)
                {
                    var genericType = _generateGeneric((GenericType)_nodes.Last().Type, (ASTNode)node.Content[0]);

                    _nodes.Add(new ExprNode("CreateGeneric", genericType));
                    PushForward();
                }
                else
                    throw new SemanticException("Unable to apply generic specifier to non-generic type", node.Position);
            }
            else if (node.Content[0].Name == "static_get")
            {
                Symbol symbol = _getStaticMember(_nodes.Last().Type, 
                    ((TokenNode)((ASTNode)node.Content[0]).Content[1]).Tok.Value, 
                    ((ASTNode)node.Content[0]).Content[0].Position,
                     ((ASTNode)node.Content[0]).Content[1].Position
                     );

                if (symbol.Modifiers.Contains(Modifier.CONSTEXPR))
                    _nodes.Add(new ConstexprNode(symbol.Name, symbol.DataType, symbol.Value));
                else
                    _nodes.Add(new IdentifierNode(symbol.Name, symbol.DataType));
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
                                _nodes.Add(new IdentifierNode(identifier, symbol.DataType));
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

                            var symbol = _getMember(((PointerType)root.Type).DataType, pointerIdentifier, node.Content[0].Position, node.Content[1].Position);
                            _nodes.Add(new ExprNode("DerefGetMember", symbol.DataType));

                            PushForward();
                            _nodes.Add(new IdentifierNode(pointerIdentifier, symbol.DataType));
                            MergeBack();
                            break;
                        }
                        else
                            throw new SemanticException("The '->' operator is not valid the given type", node.Content[0].Position);
                    case "?":
                        if (root.Type is PointerType pt)
                        {
                            string pointerIdentifier = ((TokenNode)node.Content[2]).Tok.Value;

                            var symbol = _getMember(pt.DataType, pointerIdentifier, node.Content[0].Position, node.Content[2].Position);
                            _nodes.Add(new ExprNode("NullableDerefGetMember", symbol.DataType));

                            PushForward();
                            _nodes.Add(new IdentifierNode(pointerIdentifier, symbol.DataType));
                            MergeBack();
                            break;
                        }
                        else
                            throw new SemanticException("The '?->' operator is not valid the given type", node.Content[0].Position);
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
                                    _nodes.Add(new IdentifierNode(((TokenNode)item).Tok.Value, new VoidType()));
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
                            for (int i = 0; i < initCount; i++)
                            {
                                var item = (ExprNode)_nodes[_nodes.Count - initCount + i];
                                string name = ((IdentifierNode)item.Nodes[0]).IdName;
                                if (!members.ContainsKey(name))
                                    throw new SemanticException($"Struct has no member {name}", positions[i]);
                                if (!members[name].DataType.Coerce(item.Nodes[1].Type))
                                    throw new SemanticException($"Unable to initialize member {name} with the given type", positions[i]);
                            }

                            _nodes.Add(new ExprNode("InitList", ((StructType)root.Type).GetInstance()));
                            // add in root
                            PushForward(initCount + 1);
                            break;
                        }
                        else if (root.Type is GenericType gt && gt.DataType.Classify() == TypeClassifier.STRUCT)
                        {
                            var baseStruct = (StructType)gt.DataType;

                            if (initCount != baseStruct.Members.Count)
                                throw new SemanticException("Struct initializer list must initialize all struct members", node.Content[0].Position);

                            var initMembers = Enumerable.Range(0, initCount)
                                .Select(i => (ExprNode)_nodes[_nodes.Count - initCount + i])
                                .ToDictionary(x => ((IdentifierNode)x.Nodes[0]).IdName, x => x.Type);

                            if (gt.Infer(initMembers, out List<DataType> inferredTypes))
                            {
                                gt.CreateGeneric(inferredTypes, out DataType result);

                                _nodes.Add(new ExprNode("CreateGeneric", result));

                                _nodes.RemoveAt(_nodes.Count - initCount - 1);
                                _nodes.Add(root);
                                MergeBack();

                                // no need for additional checking if inference is successful
                                _nodes.Add(new ExprNode("InitList", ((StructType)result).GetInstance()));
                                // add in root
                                PushForward();
                                // add in args
                                PushForward(initCount);
                                break;
                            }
                        }

                        throw new SemanticException("Unable to apply initializer list to the given type", node.Position);
                    case "(":
                        _visitFunctionCall(node, root.Type);
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
                    if (name == "__finalize__")
                        throw new SemanticException("Unable to directly access finalizer outside of runtime core", idPos);
                    else if (!type.GetInterface().GetFunction(name, out symbol))
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
                        throw new SemanticException($"Package has no exported symbol: `{name}`", idPos);
                    break;
                case TypeClassifier.TYPE_CLASS:
                    {
                        if (((CustomType)type).GetInstanceByName(name, out CustomNewType match))
                            symbol = new Symbol(match.Name, match);
                        else
                            throw new SemanticException($"Type class has no enumerated/value member `{name}`", idPos);
                    }
                    break;
                default:
                    throw new SemanticException("The `::` operator is not valid on the given type", opPos);
            }

            return symbol;
        }

        private void _visitFunctionCall(ASTNode node, DataType rootType)
        {
            ArgumentList args;
            if (node.Content.Count == 2)
                args = new ArgumentList();
            else
                args = _generateArgsList((ASTNode)node.Content[1]);

            bool incompletes = args.UnnamedArguments.Any(x => x is IncompleteType) || args.NamedArguments.Any(x => x.Value is IncompleteType);

            if (new[] { TypeClassifier.STRUCT, TypeClassifier.FUNCTION }.Contains(rootType.Classify()))
            {
                bool isFunction = rootType.Classify() == TypeClassifier.FUNCTION;
                FunctionType ft = null;

                if (isFunction)
                {
                    var paramData = CheckArguments((FunctionType)rootType, args);
                    ft = ((FunctionType)rootType);

                    if (paramData.IsError)
                        throw new SemanticException(paramData.ErrorMessage, paramData.ParameterPosition == -1 ? node.Position : 
                            ((ASTNode)node.Content[1]).Content.Where(x => x.Name == "arg").ToArray()[paramData.ParameterPosition].Position
                        );
                }
                else if (!isFunction && !((StructType)rootType).GetConstructor(args, out ft))
                    throw new SemanticException($"Struct `{((StructType)rootType).Name}` has no constructor the accepts the given parameters", node.Position);

                DataType returnType = isFunction ? ((FunctionType)rootType).ReturnType : ((StructType)rootType).GetInstance();

                _nodes.Add(new ExprNode(isFunction ? (((FunctionType)rootType).Async ? "CallAsync" : "Call") : "CallConstructor", returnType));

                if (args.Count() == 0)
                    PushForward();
                else
                {
                    if (incompletes)
                        // we know ft exists so no need for more in depth checking
                        _inferLambdaCallContext(args.Count(), ft);

                    PushForward(args.Count());
                    // add function to beginning of call
                    ((ExprNode)_nodes[_nodes.Count - 1]).Nodes.Insert(0, _nodes[_nodes.Count - 2]);
                    _nodes.RemoveAt(_nodes.Count - 2);
                }
            }
            else if (rootType.Classify() == TypeClassifier.GENERIC)
            {
                if (((GenericType)rootType).Infer(args, out List<DataType> inferredTypes))
                {
                    // always works - try auto eval if possible
                    ((GenericType)rootType).CreateGeneric(inferredTypes, out DataType result);

                    _nodes.Add(new ExprNode("CreateGeneric", result));
                    PushForward(args.Count() + 1);

                    // remove redundant args
                    ((TreeNode)_nodes.Last()).Nodes.RemoveRange(1, args.Count());

                    _visitFunctionCall(node, _nodes.Last().Type);
                }
                else
                    throw new SemanticException("Unable to infer types of generic arguments", node.Position);
            }
            else if (rootType is FunctionGroup fg)
            {
                if (fg.GetFunction(args, out FunctionType ft))
                {
                    _nodes.Add(new ExprNode("CallFunctionOverload", ft.ReturnType));

                    if (incompletes)
                        _inferLambdaCallContext(args.Count(), ft);

                    PushForward(args.Count() + 1);
                }
                else
                    throw new SemanticException("No function in the function group matches the given arguments", node.Position);
            }
            else if (rootType is GenericGroup gg)
            {
                if (gg.GetFunction(args, out FunctionType result))
                {
                    _nodes.Add(new ExprNode("CallGenericOverload", result.ReturnType));
                    PushForward(args.Count());

                    var root = _nodes[_nodes.Count - 2];
                    _nodes.RemoveAt(_nodes.Count - 2);

                    root = new ExprNode("CreateGeneric", result, new List<ITypeNode> { root });

                    ((TreeNode)_nodes.Last()).Nodes.Insert(0, root);
                }
                else
                    throw new SemanticException("No function in the function group matches the given arguments", node.Position);
            }
            else if (rootType.Classify() == TypeClassifier.TYPE_CLASS_INSTANCE)
            {
                if (rootType is CustomNewType cnt)
                {
                    if (cnt.Values.Count == 0)
                        throw new SemanticException("Cannot explicitly construct enumerated type class value that contains no types",
                            node.Content[1].Position);

                    if (args.NamedArguments.Count > 0)
                        throw new SemanticException("Unable to specify named values in type class value initialization",
                            node.Content[1].Position);

                    var filledGenerics = new List<DataType>();

                    if (args.Count() == cnt.Values.Count)
                    {
                        if (incompletes)
                        {
                            for (int i = args.Count(); i > 0; i--)
                            {
                                if (_nodes[_nodes.Count - i] is IncompleteNode inode)
                                {
                                    _giveContext(inode, cnt.Values[i]);

                                    args.UnnamedArguments[args.Count() - i] = _nodes.Last().Type;

                                    _nodes[_nodes.Count - i - 1] = _nodes.Last();
                                    _nodes.RemoveLast();
                                }
                            }
                        }

                        for (int i = 0; i < args.Count(); i++)
                        {
                            if (cnt.Values[i].Classify() == TypeClassifier.GENERIC_PLACEHOLDER)
                                filledGenerics.Add(args.UnnamedArguments[i]);
                            else if (!cnt.Values[i].Coerce(args.UnnamedArguments[i]))
                                throw new SemanticException("Argument types do not match up with those of type class value constructor",
                                    ((ASTNode)node.Content[1]).Content[i * 2].Position);
                        }
                    }
                    else
                        throw new SemanticException("No more and no less than all of the values of the type class " +
                            "instance must be initialized during explicit initialization", node.Position);

                    if (filledGenerics.Count > 0)
                    {
                        _table.Lookup(cnt.Parent.Name, out Symbol sym);

                        var genericType = ((GenericType)sym.DataType);

                        if (!genericType.CreateGeneric(filledGenerics, out DataType newParent))
                            throw new SemanticException("Unable to create a generic type class from the given types", node.Position);

                        ((CustomType)newParent).GetInstanceByName(cnt.Name, out cnt);
                    }

                    _nodes.Add(new ExprNode("InitTCConstructor", rootType));

                    PushForward(cnt.Values.Count);
                    PushForward();
                }
                else
                    throw new SemanticException("Unable to call non-callable type", node.Content[0].Position);
            }
            else
                throw new SemanticException("Unable to call non-callable type", node.Content[0].Position);
        }

        private void _inferLambdaCallContext(int argsCount, FunctionType ft)
        {
            for (int i = argsCount + 1; i > 1; i--)
            {
                if (_nodes[_nodes.Count - i] is IncompleteNode inode)
                {
                    var ctx = (i < ft.Parameters.Count ? ft.Parameters[i] : ft.Parameters.Last()).DataType;

                    _giveContext(inode, ctx);

                    _nodes[_nodes.Count - i - 1] = _nodes.Last();
                    _nodes.RemoveLast();
                }
            }
        }

        private void _visitSubscript(DataType rootType, ASTNode node)
        {
            bool hasStartingExpr = false, isSetContext = _isSetContext;
            int expressionCount = 0, colonCount = 0;
            var types = new List<DataType>();
            var textPositions = new List<TextPosition>();

            _isSetContext = false;

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
            else
            {
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

                            if (_isSetContext)
                                args.Add(new VoidType());

                            string methodName = $"__{(name == "Subscript" ? "[]" : "[:]")}__";
                            if (GetOverload(rootType, methodName, new ArgumentList(args), out FunctionType fnType))
                            {
                                if (isSetContext)
                                {
                                    args.RemoveLast();
                                    _setOperatorTypes.Add(fnType.Parameters.Last().DataType);                                  
                                }

                                _nodes.Add(new ExprNode(name, fnType.ReturnType));
                                // capture root as well
                                PushForward(args.Count + 1);

                                return;
                            }
                            else
                                throw new SemanticException(
                                    $"Unable to perform {(expressionCount == 1 && hasStartingExpr ? "subscript" : "slice")} on the given type",
                                    node.Position
                                 );
                        }
                        
                }

                var intType = new SimpleType(SimpleType.SimpleClassifier.INTEGER) { Constant = true };
                if (!types.All(x => intType.Coerce(x)))
                {
                    throw new SemanticException($"Invalid index type for {(expressionCount == 1 && hasStartingExpr ? "subscript" : "slice")}",
                        textPositions[Enumerable.Range(0, expressionCount).Where(x => !intType.Coerce(types[x])).First()]
                        );
                }

                _nodes.Add(new ExprNode(name, name == "Subscript" ? elementType : rootType));
                // capture root as well
                PushForward(expressionCount + 1);
            }
        }

        private void _visitHeapAlloc(ASTNode node)
        {
            if (!_couldOwnerExist)
                throw new SemanticException("Unable to dynamically allocate memory without possiblity of an owner", node.Position);

            // make ( alloc_body ) -> types , expr
            var allocBody = (ASTNode)node.Content[1];

            DataType dt = new VoidType();
            bool hasSizeExpr = false, isStructAlloc = false, isTypeAlloc = false;

            foreach (var item in allocBody.Content)
            {
                if (item.Name == "types")
                {
                    dt = _generateType((ASTNode)item);
                    _nodes.Add(new ValueNode("DataType", dt));
                    isTypeAlloc = true;
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

            if (!isStructAlloc && !isTypeAlloc && new[] { TypeClassifier.TYPE_CLASS, TypeClassifier.INTERFACE, TypeClassifier.GENERIC }
                .Contains(_nodes.Last().Type.Classify()))
            {
                isTypeAlloc = true;
                hasSizeExpr = false;
            }
            else if (!isStructAlloc && _nodes.Last().Type.Classify() == TypeClassifier.STRUCT)
                throw new SemanticException("Unable to initialize struct without a constructor", allocBody.Content[0].Position);

            if (isStructAlloc)
            {
                if (dt.Classify() != TypeClassifier.STRUCT_INSTANCE)
                    throw new SemanticException("Invalid dynamic allocation call", node.Content.Last().Position);

                _nodes.Add(new ExprNode("HeapAllocStruct", new PointerType(dt, true)));
                PushForward();
            }
            else if (isTypeAlloc)
            {
                if (new[] { TypeClassifier.LIST, TypeClassifier.DICT, TypeClassifier.FUNCTION }.Contains(dt.Classify()))
                    throw new SemanticException("Invalid data type for raw heap allocation", allocBody.Content[0].Position);

                if (!hasSizeExpr)
                    _nodes.Add(new ValueNode("Literal", new SimpleType(SimpleType.SimpleClassifier.INTEGER, true), "1"));
                else if (!new SimpleType(SimpleType.SimpleClassifier.INTEGER, true).Coerce(_nodes.Last().Type))
                    throw new SemanticException("Size of heap allocated type must be an unsigned integer", allocBody.Content[2].Position);

                _nodes.Add(new ExprNode("HeapAllocType", new PointerType(dt, true)));
                PushForward(2);
            }
            else
            {
                if (!new SimpleType(SimpleType.SimpleClassifier.INTEGER, true).Coerce(_nodes.Last().Type))
                    throw new SemanticException("Size of allocated space must be an unsigned integer", allocBody.Content[0].Position);

                _nodes.Add(new ExprNode("HeapAllocSize", new PointerType(new VoidType(), true)));
                PushForward();
            }
        }

        private void _visitFromExpr(ASTNode node)
        {
            _visitExpr((ASTNode)node.Content[1]);

            if (_nodes.Last().Type is CustomNewType cnt && cnt.Values.Count > 0)
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
