using Whirlwind.Types;
using Whirlwind.Parser;

using System.Collections.Generic;
using System.Linq;
using System;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        TextPosition _dominantPosition;

        private void _visitFunction(ASTNode function, List<Modifier> modifiers)
        {
            bool isAsync = false;
            string name = "";
            var arguments = new List<Parameter>();
            DataType dataType = new VoidType();

            TextPosition namePosition = new TextPosition();

            foreach (var item in function.Content)
            {
                switch (item.Name)
                {
                    case "TOKEN":
                        {
                            Token tok = ((TokenNode)item).Tok;

                            switch (tok.Type)
                            {
                                case "ASYNC":
                                    isAsync = true;
                                    break;
                                case "IDENTIFIER":
                                    name = tok.Value;
                                    namePosition = item.Position;
                                    break;
                                case ";":
                                    {
                                        _createFunction(arguments, dataType, name, namePosition, isAsync, modifiers);
                                        // try to complete body
                                    }
                                    break;
                            }
                        }
                        break;
                    case "args_decl_list":
                        arguments = _generateArgsDecl((ASTNode)item);
                        break;
                    case "types":
                        dataType = _generateType((ASTNode)item);
                        break;
                    case "func_body":
                        {
                            _createFunction(arguments, dataType, name, namePosition, isAsync, modifiers);

                            _nodes.Add(new IncompleteNode((ASTNode)item));
                            MergeToBlock();
                        }
                        break;
                }
            }
        }

        private void _createFunction(
            List<Parameter> parameters, DataType dataType, string name, TextPosition namePosition, bool isAsync, List<Modifier> modifiers
            )
        {
            _nodes.Add(new BlockNode(isAsync ? "AsyncFunction" : "Function"));

            var fnType = new FunctionType(parameters, dataType, isAsync) { Constant = true };

            _nodes.Add(new IdentifierNode(name, fnType));

            MergeBack();

            if (!_table.AddSymbol(new Symbol(name, fnType, modifiers)))
            {
                _table.Lookup(name, out Symbol overload);
                
                if (overload.DataType is FunctionType oft)
                {
                    if (FunctionGroup.CanDistinguish(oft, fnType))
                    {
                        overload.DataType = new FunctionGroup(new List<FunctionType> { oft, fnType }) { Constant = true };
                        return;
                    }
                }
                else if (overload.DataType is FunctionGroup)
                {
                    if (((FunctionGroup)overload.DataType).AddFunction(fnType))
                        return;
                }

                throw new SemanticException($"Unable to redeclare symbol by name {name}", namePosition);
            }
                

        }

        private void _visitFunctionBody(ASTNode body, FunctionType ft)
        {
            if (!ft.ReturnType.Coerce(_visitFuncBody(body, ft.Parameters)))
                throw new SemanticException("Return type of signature does not match return type of body", _dominantPosition);
        }

        private void _declareArgs(List<Parameter> args)
        {
            // no symbol checking necessary since params have already been full filtered and override scope
            foreach (var arg in args)
            {
                var modifiers = new List<Modifier>();

                if (arg.Volatile)
                    modifiers.Add(Modifier.VOLATILE);

                if (_isVoid(arg.DataType))
                {
                    // add va_args param
                }
                else
                {
                    _table.AddSymbol(new Symbol(arg.Name, 
                        arg.Indefinite ? new ListType(arg.DataType) : arg.DataType,
                        modifiers
                    ));
                }
            }
        }

        private DataType _visitFuncBody(ASTNode node, List<Parameter> args)
        {
            DataType rtType = new VoidType();

            if (node.Content[0].Name == "capture")
            {
                var capture = _generateCapture((ASTNode)node.Content[0]);

                _table.AddScope(capture);
            }
            else
                _table.AddScope();

            _table.DescendScope();

            _declareArgs(args);

            foreach (var item in node.Content)
            {
                switch (item.Name)
                {
                    case "func_guard":
                        _nodes.Add(new ExprNode("FunctionGuard", new VoidType()));
                        _visitExpr((ASTNode)((ASTNode)item).Content[2]);
                        MergeBack(2);
                        break;
                    case "main":
                        _visitBlock((ASTNode)item, new StatementContext(true, false, false));
                        rtType = _extractReturnType((ASTNode)item);
                        break;
                    case "expr":
                        _nodes.Add(new StatementNode("ExpressionReturn"));
                        _visitExpr((ASTNode)item);

                        _dominantPosition = item.Position;

                        rtType = _nodes.Last().Type;
                        MergeBack();

                        MergeToBlock();
                        break;
                }
            }

            _table.AscendScope();

            return rtType;
        }

        private DataType _extractReturnType(ASTNode ast)
        {
            var positions = new List<TextPosition>();
            _getReturnPositions(ast, ref positions);

            _dominantPosition = positions.Count > 0 ? positions[0] : ((ASTNode)ast.Content[0]).Content.Last().Position;

            int pos = 0;
            var returnData = _extractReturnType((BlockNode)_nodes.Last(), positions, ref pos);

            if (!returnData.Item1)
                throw new SemanticException("Inconsistent return type", positions.First());

            return returnData.Item2;
        }

        private Tuple<bool, DataType> _extractReturnType(BlockNode block, List<TextPosition> positions, ref int pos)
        {
            DataType rtType = new VoidType();
            bool returnsValue = false, setReturn = false, terminatingReturn = false;

            foreach (var node in block.Block)
            {
                if (node.Name == "Return" || node.Name == "Yield")
                {
                    var returnNode = (StatementNode)node;

                    if (returnNode.Nodes.Count > 0)
                    {
                        if (!returnsValue && setReturn)
                            throw new SemanticException("Inconsistent return types", positions[pos]);

                        DataType dt = returnNode.Nodes[0].Type;

                        if (!returnsValue)
                            rtType = dt;
                        else if (!rtType.Coerce(dt))
                        {
                            if (dt.Coerce(rtType))
                            {
                                rtType = dt;

                                _dominantPosition = positions[pos];
                            }
                            else
                            {
                                InterfaceType i1 = rtType.GetInterface(), i2 = dt.GetInterface();

                                var matches = i1.Implements.Where(x => i2.Implements.Any(y => y.Equals(x)));

                                if (matches.Count() > 0)
                                    rtType = matches.First();
                                else
                                    throw new SemanticException("Inconsistent return types", positions[pos]);
                            }
                                
                        }

                        returnsValue = true;
                    }
                    else if (returnsValue)
                        throw new SemanticException("Inconsistent return types", positions[pos]);

                    if (!setReturn)
                        setReturn = true;

                    if (!terminatingReturn)
                        terminatingReturn = true;

                    pos++;
                }
                else if (node.Name == "Throw" || node.Name == "ThrowObject")
                    terminatingReturn = true;
                else if (node is BlockNode && !node.Name.EndsWith("Function"))
                {
                    int savedPos = pos;
                    var blockReturn = _extractReturnType((BlockNode)node, positions, ref pos);
                    savedPos = (pos - savedPos) > 0 ? (pos - 1) : pos;

                    if (!blockReturn.Item1)
                        throw new SemanticException("Inconsistent return type", positions[savedPos]);

                    if (_isVoid(blockReturn.Item2))
                    {
                        if (returnsValue && setReturn)
                            throw new SemanticException("Inconsistent return type", positions[savedPos]);
                        else
                            continue;
                    }

                    if (!rtType.Coerce(blockReturn.Item2))
                    {
                        if (blockReturn.Item2.Coerce(rtType))
                            rtType = blockReturn.Item2;
                        else
                        {
                            InterfaceType i1 = rtType.GetInterface(), i2 = blockReturn.Item2.GetInterface();

                            var matches = i1.Implements.Where(x => i2.Implements.Contains(x));

                            if (matches.Count() > 0)
                                rtType = matches.First();
                            else
                                throw new SemanticException("Inconsistent return type", positions[savedPos]);
                        }
                            
                    }

                    if (!terminatingReturn && blockReturn.Item1)
                        terminatingReturn = true;

                    if (!setReturn)
                        setReturn = true;

                    if (!returnsValue)
                        returnsValue = true;
                }
            }

            return new Tuple<bool, DataType>(!returnsValue || terminatingReturn, rtType);
        }

        private void _getReturnPositions(ASTNode node, ref List<TextPosition> positions)
        {
            foreach (var item in node.Content)
            {
                if (item.Name == "return_stmt" || item.Name == "yield_stmt")
                    positions.Add(item.Position);
                else if (item.Name != "TOKEN" && item.Name != "main")
                    _getReturnPositions((ASTNode)item, ref positions);
            }
        }

        public List<Parameter> _generateArgsDecl(ASTNode node)
        {
            var argsDeclList = new List<Parameter>(); 
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "decl_arg")
                {
                    bool optional = false,
                        hasExtension = false,
                        isVolatile = false;
                    var identifiers = new List<string>();
                    DataType paramType = new VoidType();

                    foreach (var argPart in ((ASTNode)subNode).Content)
                    {
                        switch (argPart.Name)
                        {
                            case "TOKEN":
                                switch (((TokenNode)argPart).Tok.Type)
                                {
                                    case "IDENTIFIER":
                                        identifiers.Add(((TokenNode)argPart).Tok.Value);
                                        break;
                                    case "VOL":
                                        isVolatile = true;
                                        break;
                                }

                                break;
                            case "extension":
                                paramType = _generateType((ASTNode)((ASTNode)argPart).Content[1]);
                                hasExtension = true;
                                break;
                            case "initializer":
                                _visitExpr((ASTNode)((ASTNode)argPart).Content[1]);

                                if (!hasExtension)
                                    paramType = _nodes.Last().Type;

                                optional = true;
                                break;
                        }
                    }

                    if (!optional && !hasExtension)
                        throw new SemanticException("Unable to create argument with no type", subNode.Position);

                    if (hasExtension && optional && !paramType.Coerce(_nodes.Last().Type))
                        throw new SemanticException("Initializer type incompatable with type extension", subNode.Position);

                    if (optional)
                    {
                        foreach (var identifier in identifiers)
                            argsDeclList.Add(new Parameter(identifier, paramType, true, false, isVolatile, _nodes.Last()));

                        _nodes.RemoveAt(_nodes.Count - 1); // remove argument from node stack
                    }
                    else
                    {
                        foreach (var identifier in identifiers)
                            argsDeclList.Add(new Parameter(identifier, paramType, false, false, isVolatile));
                    }
                }
                else if (subNode.Name == "ending_arg")
                {
                    string name = "";
                    DataType dt = new VoidType();

                    foreach (var item in ((ASTNode)subNode).Content)
                    {
                        if (item.Name == "extension")
                            dt = _generateType((ASTNode)((ASTNode)item).Content[1]);
                        else if (item.Name == "TOKEN" && ((TokenNode)item).Tok.Type == "IDENTIFIER")
                            name = ((TokenNode)item).Tok.Value;
                    }

                    argsDeclList.Add(new Parameter(name, dt, false, true, false));
                }
            }

            if (argsDeclList.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                throw new SemanticException("Function cannot be declared with duplicate arguments", node.Position);

            return argsDeclList;
        }

        // generate a argument list from a function call and generate the corresponding tree
        private ArgumentList _generateArgsList(ASTNode node)
        {
            var uArgs = new List<DataType>();
            var nArgs = new Dictionary<string, DataType>();

            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "arg")
                {
                    var argNode = (ASTNode)((ASTNode)subNode).Content.First();

                    if (argNode.Name == "expr")
                    {
                        // named args, before all unnamed args
                        if (nArgs.Count > 0)
                            throw new SemanticException("Named arguments must be declared after unnamed arguments",
                                subNode.Position);

                        _visitExpr(argNode);
                        uArgs.Add(_nodes.Last().Type);
                    }
                    else if (argNode.Name == "named_arg")
                    {
                        string name = ((TokenNode)argNode.Content[0]).Tok.Value;

                        _visitExpr((ASTNode)argNode.Content[2]);
                        DataType dt = _nodes.Last().Type;

                        _nodes.Add(new ExprNode("NamedArgument", dt));
                        PushForward();

                        nArgs[name] = dt;
                    }
                }
            }

            return new ArgumentList(uArgs, nArgs);
        }
    }
}
