using Whirlwind.Parser;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitDecorator(ASTNode node, List<Modifier> modifiers)
        {
            var fnNode = (ASTNode)node.Content[1];

            if (fnNode.Content[2].Name == "generic_tag")
                throw new SemanticException("Unable to apply decorator to a generic function", fnNode.Content[2].Position);

            _visitFunction(fnNode, modifiers);

            FunctionType startFn = ((FunctionType)((TreeNode)_nodes.Last()).Nodes[0].Type).MutableCopy();

            _nodes.Add(new BlockNode("Decorator"));

            foreach (var item in ((ASTNode)node.Content[0]).Content)
            {
                if (item.Name == "decor_expr")
                {
                    _visitDecorExpr((ASTNode)item, startFn);

                    if (_nodes.Last().Type.Classify() == TypeClassifier.FUNCTION)
                    {
                        FunctionType decorType = (FunctionType)_nodes.Last().Type;

                        if (_nodes.Last().Name == "DecoratorCall" || decorType.MatchArguments(new ArgumentList(new List<DataType>() { startFn })))
                        {
                            // check for non-function decorators
                            if (!(decorType.ReturnType is FunctionType))
                                throw new SemanticException("A decorator must return a function", item.Position);
                            // check for non-constant decorator
                            else if (!decorType.ReturnType.Constant)
                                throw new SemanticException("Return type of decorator must be constant", item.Position);

                            // allows decorator to override function return type ;)
                            if (!startFn.Coerce(decorType.ReturnType))
                            {
                                _table.Lookup(((TokenNode)((ASTNode)node.Content[1]).Content[1]).Tok.Value, out Symbol sym);

                                if (sym.DataType is FunctionGroup fg)
                                    fg.Functions = fg.Functions.Select(x => x.Equals(startFn) ? decorType.ReturnType : x)
                                        .Select(x => (FunctionType)x).ToList();
                                else
                                    sym.DataType = decorType.ReturnType;
                            }

                            MergeBack();
                        }
                        else
                            throw new SemanticException("This decorator is not valid for the given function", item.Position);
                    }
                    else
                        throw new SemanticException("Unable to use non-function as a decorator", item.Position);
                }
            }

            PushToBlock();
        }

        private void _visitDecorExpr(ASTNode decorExpr, FunctionType baseFn)
        {
            bool noArgsList = true;

            foreach (var item in decorExpr.Content)
            {
                switch (item.Name)
                {
                    case "TOKEN":
                        {
                            var tkNode = ((TokenNode)item);

                            if (tkNode.Tok.Type == "IDENTIFIER")
                            {
                                if (_table.Lookup(tkNode.Tok.Value, out Symbol sym))
                                    _nodes.Add(new IdentifierNode(sym.Name, sym.DataType));
                                else
                                    throw new SemanticException($"Undefined Symbol: `{tkNode.Tok.Value}`", tkNode.Position);
                            }
                            else if (tkNode.Tok.Type == ")" && noArgsList)
                                throw new SemanticException("Decorator function call must contain at least 1 argument", item.Position);
                        }
                        break;
                    case "static_get":
                        if (_nodes.Last().Type is Package pkg)
                        {
                            var getId = (TokenNode)((ASTNode)item).Content[1];

                            if (pkg.Lookup(getId.Tok.Value, out Symbol sym))
                            {
                                _nodes.Add(new IdentifierNode(sym.Name, sym.DataType));

                                _nodes.Add(new ExprNode("StaticGet", sym.DataType));
                                PushForward(2);
                            }
                            else
                                throw new SemanticException($"Package has no exported symbol: `{getId.Tok.Value}`", getId.Position);
                        }
                        else
                            throw new SemanticException("Unable to use static-get operator on non-package in decorator expression", item.Position);
                        break;
                    case "generic_spec":
                        if (_nodes.Last().Type.Classify() == TypeClassifier.GENERIC)
                        {
                            var genericType = _generateGeneric((GenericType)_nodes.Last().Type, (ASTNode)item);

                            _nodes.Add(new ExprNode("CreateGeneric", genericType));
                            PushForward();
                        }
                        else
                            throw new SemanticException("Unable to apply generic specifier to non-generic type", item.Position);
                        break;
                    case "args_list":
                        {
                            noArgsList = false;

                            var startType = _nodes.Last().Type;
                            var args = _generateArgsList((ASTNode)item);

                            args.UnnamedArguments.Insert(0, baseFn);

                            FunctionType fnType;
                            if (startType is FunctionGroup fg && !fg.GetFunction(args, out fnType))
                                throw new SemanticException("No function in the function group matches the given arguments", item.Position);
                            else if (startType is FunctionType ft)
                                fnType = ft;
                            else if (startType is GenericType gt)
                            {
                                if (gt.DataType.Classify() != TypeClassifier.FUNCTION)
                                    throw new SemanticException("Unable to use non-function as a decorator", decorExpr.Position);

                                if (gt.Infer(args, out List<DataType> inferredTypes))
                                {
                                    gt.CreateGeneric(inferredTypes, out DataType result);

                                    var insertNode = _nodes[_nodes.Count - args.Count()];
                                    _nodes.RemoveAt(_nodes.Count - args.Count());

                                    _nodes.Add(new ExprNode("CreateGeneric", result));
                                    _nodes.Add(insertNode);
                                    MergeBack();

                                    var createNode = _nodes[_nodes.Count - 1];
                                    _nodes.RemoveAt(_nodes.Count - 1);

                                    _nodes.Insert(_nodes.Count - args.Count() + 1, createNode);

                                    fnType = (FunctionType)result;
                                }
                                else
                                    throw new SemanticException("Unable to infer types of generic arguments", item.Position);
                            }
                            else
                                throw new SemanticException("Unable to use non-function as a decorator", decorExpr.Position);

                            var paramData = CheckArguments(fnType, args);

                            if (paramData.IsError)
                            {
                                if (paramData.ParameterPosition == 0)
                                    throw new SemanticException("This decorator is not valid for the given function", decorExpr.Position);
                                else if (paramData.ParameterPosition == -1)
                                    throw new SemanticException(paramData.ErrorMessage, decorExpr.Position);
                                else
                                {
                                    throw new SemanticException(paramData.ErrorMessage, ((ASTNode)item).Content
                                        .Where(x => x.Name == "arg")
                                        .ToArray()[paramData.ParameterPosition - 1]
                                        .Position
                                        );
                                }
                            }

                            bool incompletes = args.UnnamedArguments.Skip(1).Any(x => x is IncompleteType)
                                || args.NamedArguments.Any(x => x.Value is IncompleteType);

                            _nodes.Add(new ExprNode("DecoratorCall", fnType));

                            if (args.Count() == 0)
                                PushForward();
                            else
                            {
                                if (incompletes)
                                    // we know fnType exists so no need for more in depth checking
                                    _inferLambdaCallContext(args.Count() - 1, fnType);

                                PushForward(args.Count() - 1);
                                // add function to beginning of call
                                ((ExprNode)_nodes[_nodes.Count - 1]).Nodes.Insert(0, _nodes[_nodes.Count - 2]);
                                _nodes.RemoveAt(_nodes.Count - 2);
                            }
                        }
                        break;
                }
            }
        }
    }
}
