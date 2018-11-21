using Whirlwind.Types;
using Whirlwind.Parser;

using System.Collections.Generic;
using System.Linq;
using System;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitFunction(ASTNode function)
        {
            bool isAsync = false;
            string name = "";
            var parameters = new List<Parameter>();
            IDataType dataType = new SimpleType();
            var namePosition = new TextPosition();

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
                                        _createFunction(parameters, dataType, name, namePosition, isAsync);
                                        // try to complete body
                                    }
                                    break;
                            }
                        }
                        break;
                    case "args_decl_list":
                        parameters = _generateArgsDecl((ASTNode)item);
                        break;
                    case "types":
                        dataType = _generateType((ASTNode)item);
                        break;
                    case "func_body":
                        {
                            _createFunction(parameters, dataType, name, namePosition, isAsync);

                            _table.AddScope();
                            _table.DescendScope();

                            // no symbol checking necessary since params have already been full filtered and override scope
                            foreach (var param in parameters)
                            {
                                if (param.Indefinite)
                                {
                                    if (param.DataType.Classify() == TypeClassifier.SIMPLE && ((SimpleType)param.DataType).Type == SimpleType.DataType.VOID)
                                    {
                                        // add va_args param
                                    }
                                    else
                                    {
                                        _table.AddSymbol(new Symbol(
                                            param.Name,
                                            new ListType(param.DataType),
                                            param.Constant ? new List<Modifier>() { Modifier.CONSTANT } : new List<Modifier>()
                                            ));
                                    }
                                }
                                else
                                {
                                    _table.AddSymbol(new Symbol(
                                        param.Name,
                                        param.DataType,
                                        param.Constant ? new List<Modifier>() { Modifier.CONSTANT } : new List<Modifier>() 
                                    ));
                                }
                            }

                            _visitFuncBody((ASTNode)item);
                            _table.AscendScope();
                        }
                        break;
                }
            }
        }

        private void _createFunction(
            List<Parameter> parameters, IDataType dataType, string name, TextPosition namePosition, bool isAsync
            )
        {
            _nodes.Add(new BlockNode(isAsync ? "AsyncFunction" : "Function"));

            var fnType = new FunctionType(parameters, dataType, isAsync);

            _nodes.Add(new IdentifierNode(name, fnType, true));

            MergeBack();

            if (!_table.AddSymbol(new Symbol(name, fnType, 
                _exported ? new List<Modifier>() { Modifier.EXPORTED, Modifier.CONSTANT }
                : new List<Modifier>() { Modifier.CONSTANT })))
                throw new SemanticException($"Unable to redeclare symbol by name {name}", namePosition);
        }

        private IDataType _visitFuncBody(ASTNode node)
        {
            IDataType rtType = new SimpleType();

            foreach (var item in node.Content)
            {
                switch (item.Name)
                {
                    case "func_guard":
                        _nodes.Add(new ExprNode("FunctionGuard", new SimpleType()));
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

                        rtType = _nodes.Last().Type;
                        MergeBack();

                        MergeToBlock();
                        break;
                }
            }

            return rtType;
        }

        private IDataType _extractReturnType(ASTNode ast)
        {
            var positions = new List<TextPosition>();
            _getReturnPositions(ast, ref positions);

            int pos = 0;
            var returnData = _extractReturnType((BlockNode)_nodes.Last(), positions, ref pos);

            if (!returnData.Item1)
                throw new SemanticException("Inconsistent return type", positions.First());

            return returnData.Item2;
        }

        private Tuple<bool, IDataType> _extractReturnType(BlockNode block, List<TextPosition> positions, ref int pos)
        {
            IDataType rtType = new SimpleType();
            bool returnsValue = false, setReturn = false, terminatingReturn = false;

            foreach (var node in block.Block)
            {
                if (node.Name == "Return" || node.Name == "Yield")
                {
                    var typeList = new List<IDataType>();

                    foreach (var expr in ((StatementNode)node).Nodes)
                    {
                        typeList.Add(expr.Type);
                    }

                    if (typeList.Count > 0)
                    {
                        if (!returnsValue && setReturn)
                            throw new SemanticException("Inconsistent return types", positions[pos]);

                        IDataType dt = typeList.Count == 1 ? typeList[0] : new TupleType(typeList);

                        if (!rtType.Coerce(dt))
                        {
                            if (dt.Coerce(rtType))
                                rtType = dt;
                            else
                                throw new SemanticException("Inconsistent return types", positions[pos]);
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
                else if (node is BlockNode)
                {
                    int savedPos = pos;
                    var blockReturn = _extractReturnType((BlockNode)node, positions, ref pos);
                    savedPos = (pos - savedPos) > 0 ? (pos - 1) : pos;

                    if (blockReturn.Item2.Classify() == TypeClassifier.SIMPLE && ((SimpleType)blockReturn.Item2).Type == SimpleType.DataType.VOID)
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
                            throw new SemanticException("Inconsistent return type", positions[savedPos]);
                    }

                    if (!terminatingReturn && blockReturn.Item1)
                        terminatingReturn = true;

                    if (!setReturn)
                        setReturn = true;

                    if (!returnsValue)
                        returnsValue = true;
                        
                }
            }

            return new Tuple<bool, IDataType>(!returnsValue || terminatingReturn, rtType);
        }

        private void _getReturnPositions(ASTNode node, ref List<TextPosition> positions)
        {
            foreach (var item in node.Content)
            {
                if (item.Name == "return_stmt" || item.Name == "yield_stmt")
                    positions.Add(item.Position);
                else if (item.Name != "TOKEN")
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
                        constant = false,
                        setParamType = false;
                    var identifiers = new List<string>();
                    IDataType paramType = new SimpleType();

                    foreach (var argPart in ((ASTNode)subNode).Content)
                    {
                        switch (argPart.Name)
                        {
                            case "TOKEN":
                                if (((TokenNode)argPart).Tok.Type == "IDENTIFIER")
                                    identifiers.Add(((TokenNode)argPart).Tok.Value);
                                break;
                            case "arg_ext":
                                foreach (var extensionPart in ((ASTNode)argPart).Content)
                                {
                                    if (extensionPart.Name == "types")
                                    {
                                        paramType = _generateType((ASTNode)extensionPart);
                                        setParamType = true;
                                    }
                                    else if (extensionPart.Name == "TOKEN")
                                    {
                                        if (((TokenNode)extensionPart).Tok.Type == "@")
                                            constant = true;
                                    }
                                }
                                
                                break;
                            case "initializer":
                                _visitExpr((ASTNode)((ASTNode)argPart).Content[1]);
                                optional = true;
                                break;
                        }
                    }

                    if (!optional && !setParamType)
                        throw new SemanticException("Unable to create argument with no type", subNode.Position);

                    if (setParamType && optional && !paramType.Coerce(_nodes.Last().Type))
                        throw new SemanticException("Initializer type incompatable with type extension", subNode.Position);

                    if (optional)
                    {
                        foreach (var identifier in identifiers)
                            argsDeclList.Add(new Parameter(identifier, paramType, false, constant, _nodes.Last()));

                        _nodes.RemoveAt(_nodes.Count - 1); // remove argument from node stack
                    }
                    else
                    {
                        foreach (var identifier in identifiers)
                            argsDeclList.Add(new Parameter(identifier, paramType, false, constant));
                    }
                }
                else if (subNode.Name == "ending_arg")
                {
                    bool constant = false;
                    string name = "";
                    IDataType dt = new SimpleType();

                    foreach (var item in ((ASTNode)subNode).Content)
                    {
                        if (item.Name == "arg_ext")
                        {
                            foreach (var extensionPart in ((ASTNode)item).Content)
                            {
                                if (extensionPart.Name == "types")
                                    dt = _generateType((ASTNode)extensionPart);
                                else if (extensionPart.Name == "TOKEN")
                                {
                                    if (((TokenNode)extensionPart).Tok.Type == "@")
                                        constant = true;
                                }
                            }
                        }
                        else if (item.Name == "TOKEN" && ((TokenNode)item).Tok.Type == "IDENTIFIER")
                            name = ((TokenNode)item).Tok.Value;
                    }

                    argsDeclList.Add(new Parameter(name, dt, true, constant));
                }
            }

            if (argsDeclList.GroupBy(x => x.Name).Any(x => x.Count() > 1))
                throw new SemanticException("Function cannot be declared with duplicate arguments", node.Position);

            return argsDeclList;
        }

        // generate a parameter list from a function call and generate the corresponding tree
        private List<ParameterValue> _generateArgsList(ASTNode node)
        {
            var argsList = new List<ParameterValue>();
            foreach (var subNode in node.Content)
            {
                if (subNode.Name == "arg")
                {
                    var arg = (ASTNode)subNode;

                    if (arg.Content[0].Name == "expr")
                    {
                        _visitExpr((ASTNode)arg.Content[0]);
                        argsList.Add(new ParameterValue(_nodes.Last().Type));
                    }
                    else
                    {
                        string name = ((TokenNode)arg.Content[0]).Tok.Value;
                        _visitExpr((ASTNode)((ASTNode)arg.Content[1]).Content[1]);
                        _nodes.Add(new ExprNode("NamedArgument", _nodes.Last().Type, new List<ITypeNode>() {
                            new IdentifierNode(name, new SimpleType(), false)
                        }));
                        PushForward();

                        argsList.Add(new ParameterValue(name, _nodes.Last().Type));
                    }
                }
            }

            if (argsList.Where(x => x.HasName).GroupBy(x => x.Name).Any(x => x.Count() > 1))
                throw new SemanticException("Unable to initialize named argument with two different values", node.Position);
            return argsList;
        }
    }
}
