using Whirlwind.Types;
using Whirlwind.Parser;

using System;
using System.Collections.Generic;
using System.Linq;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitVarDecl(ASTNode stmt, List<Modifier> modifiers)
        {
            bool constant = false, constexpr = false, hasType = false, hasInitializer = false;
            IDataType mainType = new SimpleType();

            var variables = new Dictionary<string, Tuple<IDataType, TextPosition>>();
            var initializers = new Dictionary<string, Tuple<bool, ITypeNode>>();

            foreach (var item in stmt.Content)
            {
                switch (item.Name)
                {
                    case "TOKEN":
                        if (((TokenNode)item).Tok.Type == "@")
                            constant = true;
                        break;
                    case "var":
                        {
                            var variableBlock = (ASTNode)item;

                            if (variableBlock.Content.Count == 1)
                                variables[((TokenNode)variableBlock.Content[0]).Tok.Value]
                                    = new Tuple<IDataType, TextPosition>(new SimpleType(), variableBlock.Content[0].Position);
                            else
                            {
                                string currentIdentifier = "";
                                var currentPosition = new TextPosition();

                                foreach (var elem in variableBlock.Content)
                                {
                                    switch (elem.Name)
                                    {
                                        case "TOKEN":
                                            if (elem.Name == "IDENTIFIER")
                                            {
                                                currentIdentifier = ((TokenNode)elem).Tok.Value;
                                                currentPosition = elem.Position;
                                            }
                                            else if (elem.Name == ",")
                                            {
                                                if (!variables.ContainsKey(currentIdentifier))
                                                    variables[currentIdentifier] = new Tuple<IDataType, TextPosition>(new SimpleType(), currentPosition);
                                            }
                                            break;
                                        case "extension":
                                            IDataType dt = _generateType((ASTNode)((ASTNode)elem).Content[1]);
                                            variables[currentIdentifier] = new Tuple<IDataType, TextPosition>(dt, currentPosition);
                                            break;
                                        case "variable_initializer":
                                            {
                                                _visitExpr((ASTNode)((ASTNode)elem).Content[1]);

                                                if (((TokenNode)((ASTNode)elem).Content[0]).Tok.Type == ":=")
                                                {
                                                    if (!Constexpr.Evaluator.TryEval(_nodes.Last()))
                                                        throw new SemanticException("Non constexpr value with constexpr initializer.", item.Position);
                                                    else
                                                    {
                                                        ITypeNode node = _nodes.Last();
                                                        _nodes.RemoveAt(_nodes.Count - 1);

                                                        ValueNode valNode = Constexpr.Evaluator.Evaluate(node);

                                                        _nodes.Add(valNode);
                                                    }

                                                    constexpr = true;
                                                }

                                                _nodes.Add(new ExprNode(constexpr ? "ConstExprInitializer" : "Initializer", _nodes.Last().Type));
                                                PushForward();

                                                var initializer = _nodes.Last();
                                                _nodes.RemoveAt(_nodes.Count - 1);

                                                if (!variables.ContainsKey(currentIdentifier))
                                                    variables[currentIdentifier] = new Tuple<IDataType, TextPosition>(initializer.Type, currentPosition);

                                                initializers[currentIdentifier]
                                                    = new Tuple<bool, ITypeNode>(((TokenNode)((ASTNode)item).Content[1]).Tok.Type == ":=", initializer);
                                            }
                                            break;
                                    }
                                }
                            }
                        }
                        break;
                    case "extension":
                        mainType = _generateType((ASTNode)((ASTNode)item).Content[1]);
                        hasType = true;
                        break;
                    case "variable_initializer":
                        _visitExpr((ASTNode)((ASTNode)item).Content[1]);

                        if (((TokenNode)((ASTNode)item).Content[0]).Tok.Type == ":=")
                        {
                            if (!Constexpr.Evaluator.TryEval(_nodes.Last()))
                                throw new SemanticException("Non constexpr value with constexpr initializer.", item.Position);
                            else
                            {
                                ITypeNode node = _nodes.Last();
                                _nodes.RemoveAt(_nodes.Count - 1);

                                ValueNode valNode = Constexpr.Evaluator.Evaluate(node);

                                _nodes.Add(valNode);
                            }

                            constexpr = true;
                        }

                        _nodes.Add(new ExprNode(constexpr ? "ConstExprInitializer" : "Initializer", _nodes.Last().Type));
                        PushForward();

                        if (!hasType)
                            mainType = _nodes.Last().Type;
                        else if (!mainType.Coerce(_nodes.Last().Type))
                            throw new SemanticException("Initializer type doesn't match type extension", item.Position);

                        hasInitializer = true;
                        break;
                }
            }

            bool isVoid(IDataType dt) => dt.Classify() == TypeClassifier.SIMPLE && ((SimpleType)dt).Type == SimpleType.DataType.VOID;

            if (hasType && hasInitializer && mainType.Classify() == TypeClassifier.TUPLE)
            {
                TupleType tupleType = (TupleType)mainType;

                if (variables.Count == tupleType.Types.Count)
                {
                    using (var e1 = variables.GetEnumerator())
                    using (var e2 = tupleType.Types.GetEnumerator())
                    {
                        while (e1.MoveNext() && e2.MoveNext())
                        {
                            if (initializers.ContainsKey(e1.Current.Key))
                                throw new SemanticException("Unable to perform tuple based initialization on pre initialized values", e1.Current.Value.Item2);
                            else if (isVoid(e1.Current.Value.Item1))
                                variables[e1.Current.Key] = new Tuple<IDataType, TextPosition>(e2.Current, e1.Current.Value.Item2);
                            else if (!e1.Current.Value.Item1.Coerce(e2.Current))
                                throw new SemanticException("Tuple types and variable types must match", e1.Current.Value.Item2);
                        }
                    }
                }
                else
                    throw new SemanticException("The number of variables must match the size of tuple being assigned", stmt.Position);
            }
            else
            {
                foreach (var item in variables)
                {
                    if (isVoid(item.Value.Item1))
                    {
                        if (hasType)
                            variables[item.Key] = new Tuple<IDataType, TextPosition>(mainType, item.Value.Item2);
                        else
                            throw new SemanticException("Unable to infer type of variable", item.Value.Item2);
                    }
                }
            }

            foreach (var variable in variables.Keys)
            {
                if (variable == "_")
                    continue;

                Symbol symbol;

                if (initializers.ContainsKey(variable) && initializers[variable].Item1)
                    symbol = new Symbol(variable, variables[variable].Item1, ((ValueNode)((ExprNode)initializers[variable].Item2).Nodes[0]).Value);
                else if (constexpr && !initializers.ContainsKey(variable))
                    // last item on stack will always be the main initializer if constexpr
                    symbol = new Symbol(variable, variables[variable].Item1, ((ValueNode)((ExprNode)_nodes.Last()).Nodes[0]).Value);
                else
                    symbol = new Symbol(variable, variables[variable].Item1);

                foreach (var modifier in modifiers)
                    symbol.Modifiers.Add(modifier);

                if (constant)
                    symbol.Modifiers.Add(Modifier.CONSTANT);

                if (!_table.AddSymbol(symbol))
                    throw new SemanticException("Variable declared multiple times in the current scope", variables[variable].Item2);

                _nodes.Add(new IdentifierNode(variable, variables[variable].Item1, constant));

                if (initializers.ContainsKey(variable))
                {
                    _nodes.Add(initializers[variable].Item2);
                    _nodes.Add(new ExprNode("Var", variables[variable].Item1));
                    PushForward(2);
                }
                else
                {
                    _nodes.Add(new ExprNode("Var", variables[variable].Item1));
                    PushForward();
                }
            }

            _nodes.Add(new ExprNode("Variables", new SimpleType()));
            PushForward(variables.Keys.Where(x => x != "_").Count());

            string statementName;

            if (constexpr)
                statementName = "DeclareConstexpr";
            else if (constant)
                statementName = "DeclareConstant";
            else
                statementName = "DeclareVariable";

            _nodes.Add(new StatementNode(statementName));
            PushForward(2);
            ((StatementNode)_nodes[_nodes.Count - 1]).Nodes.Reverse();
        }
    }
}
