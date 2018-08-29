using Whirlwind.Parser;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

using System;
using System.Linq;
using System.Collections.Generic;

namespace Whirlwind.Semantic.Visitor
{
    struct StatementContext
    {
        public bool Function;
        public bool BreakValid;
        public bool ContinueValid;

        public StatementContext(bool fn, bool breakValid, bool continueValid)
        {
            Function = fn;
            BreakValid = breakValid;
            ContinueValid = continueValid;
        }
    }

    partial class Visitor
    {
        private void _visitStatement(ASTNode node, StatementContext context)
        {
            ASTNode stmt = (ASTNode)node.Content[0];

            // block bubbling statements
            switch (stmt.Name)
            {
                case "local_variable_decl":
                    break;
                case "enum_const":
                    _visitEnumConst(stmt);
                    break;
            }

            // statement bubbling statements
            try
            {
                switch (stmt.Name)
                {
                    case "assignment":
                        break;
                    case "expr":
                        _visitExpr(stmt);
                        _nodes.Add(new StatementNode("ExprStmt"));
                        PushForward();
                        break;
                    case "break_stmt":
                        if (context.BreakValid)
                            _nodes.Add(new StatementNode("Break"));
                        else
                            throw new SemanticException("Invalid context for a break statement", stmt.Position);
                        break;
                    case "continue_stmt":
                        if (context.ContinueValid)
                            _nodes.Add(new StatementNode("Continue"));
                        else
                            throw new SemanticException("Invalid context for a continue statement", stmt.Position);
                        break;
                    case "throw_stmt":
                        ASTNode throwExpr = (ASTNode)stmt.Content[1];
                        _visitExpr(throwExpr);

                        if (IsException(_nodes.Last().Type))
                            _nodes.Add(new StatementNode("ThrowException"));
                        else
                            _nodes.Add(new StatementNode("ThrowObject"));

                        PushForward();
                        break;
                    // return and yield types checked later
                    case "return_stmt":
                        if (context.Function)
                            _visitReturn(stmt, "Return");
                        else
                            throw new SemanticException("Invalid context for a return statement", stmt.Position);
                        break;
                    case "yield_stmt":
                        if (context.Function)
                            _visitReturn(stmt, "Yield");
                        else
                            throw new SemanticException("Invalid context for a return statement", stmt.Position);
                        break;
                    case "delete_stmt":
                        var identifierList = new List<ITypeNode>();

                        foreach (var subNode in stmt.Content)
                        {
                            if (subNode.Name == "TOKEN")
                            {
                                var token = ((TokenNode)subNode).Tok;

                                if (token.Type == "IDENTIFIER")
                                {
                                    if (_table.Lookup(token.Value, out Symbol sym))
                                    {
                                        if (sym.DataType.Classify() == "POINTER")
                                            identifierList.Add(new IdentifierNode(sym.Name, sym.DataType, sym.Modifiers.Contains(Modifier.CONSTANT)));
                                        else
                                            throw new SemanticException("Delete statement must only contain pointers", subNode.Position);
                                    }
                                    else
                                        throw new SemanticException($"Undefined identifier `{token.Value}`", subNode.Position);
                                }
                            }
                        }

                        _nodes.Add(new StatementNode("Delete", identifierList));
                        break;
                }
            }
            catch (SemanticException se) {
                _errorQueue.Add(se);
            }
        }

        // visit return and yield statements
        public void _visitReturn(ASTNode stmt, string stmtName)
        {
            int exprCount = 0;

            foreach (var subNode in stmt.Content)
            {
                if (subNode.Name == "expr")
                {
                    _visitExpr((ASTNode)subNode);
                    exprCount++;
                }
            }

            if (exprCount == 0)
                _nodes.Add(new StatementNode(stmtName, new List<ITypeNode>() { new ValueNode("Literal", new SimpleType(), "null") }));
            else if (exprCount == 1)
            {
                _nodes.Add(new StatementNode(stmtName));
                PushForward();
            }
            else
            {
                var tupleTypes = new List<IDataType>();

                for (int i = 0; i < exprCount; i++)
                {
                    tupleTypes.Add(_nodes[_nodes.Count - (i + 1)].Type);
                }

                _nodes.Add(new ExprNode("Tuple", new TupleType(tupleTypes)));
                PushForward(exprCount);
                _nodes.Add(new StatementNode(stmtName));
                PushForward();
            }
        }

        private void _visitEnumConst(ASTNode node)
        {
            bool constexpr = true;
            int idCount = 0;
            int startValue = 0;

            foreach (var item in node.Content)
            {
                if (item.Name == "first_const")
                {
                    string name = ((TokenNode)((ASTNode)item).Content[0]).Tok.Value;

                    _nodes.Add(new IdentifierNode(name,
                            new SimpleType(SimpleType.DataType.INTEGER, true),
                            true
                        ));

                    if (((ASTNode)item).Content.Count == 2)
                    {
                        // first_const -> variable_intializer
                        var initializer = (ASTNode)((ASTNode)item).Content[1];

                        if (((TokenNode)initializer.Content[0]).Tok.Type == "=")
                            constexpr = false;

                        _visitExpr((ASTNode)initializer.Content[1]);

                        // catch type exception first and then calculate constexpr values
                        if (!new SimpleType(SimpleType.DataType.INTEGER, true).Coerce(_nodes.Last().Type))
                            throw new SemanticException("Enumerated constant must have an unsigned integer initializer", initializer.Position);

                        if (constexpr)
                            if (!Constexpr.Evaluator.TryEval((ExprNode)_nodes.Last()))
                                throw new SemanticException("Value of constexpr initializer must a be constexpr", initializer.Content[0].Position);
                            else
                                startValue = Int32.Parse(Constexpr.Evaluator.Evaluate((ExprNode)_nodes.Last()).Value);

                        _nodes.Add(new ExprNode("EnumConstInitializer", new SimpleType(SimpleType.DataType.INTEGER, true)));
                        PushForward(2);
                    }

                    var symbol = constexpr ? new Symbol(name, _nodes.Last().Type, startValue.ToString())
                        : new Symbol(name, _nodes.Last().Type);

                    if (!_table.AddSymbol(symbol))
                        throw new SemanticException($"Variable `{name}` redeclared in scope", ((ASTNode)item).Content[0].Position);

                    idCount++;
                }
                else if (item.Name == "TOKEN" && ((TokenNode)item).Tok.Type == "IDENTIFIER")
                {
                    _nodes.Add(new IdentifierNode(
                        ((TokenNode)item).Tok.Value, new SimpleType(SimpleType.DataType.INTEGER, true),
                        true
                        ));

                    var symbol = constexpr ? new Symbol(((TokenNode)item).Tok.Value, _nodes.Last().Type, (startValue + idCount).ToString())
                        : new Symbol(((TokenNode)item).Tok.Value, _nodes.Last().Type);

                    if (!_table.AddSymbol(symbol))
                        throw new SemanticException($"Variable `{((TokenNode)item).Tok.Value}` redeclared in scope", item.Position);

                    idCount++;
                }
            }

            _nodes.Add(new StatementNode(constexpr ? "EnumConstExpr" : "EnumConst"));
            PushForward(idCount);
        }
    }
}
