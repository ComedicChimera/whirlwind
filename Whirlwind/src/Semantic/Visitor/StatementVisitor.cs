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
                    {
                        var modifiers = new List<Modifier>();

                        foreach (var item in stmt.Content)
                        {
                            if (item.Name == "TOKEN")
                            { 
                                modifiers.Add(Modifier.STATIC);
                            }
                            else
                                _visitVarDecl((ASTNode)item, modifiers);
                        }
                    }
                    return;
                case "enum_const":
                    _visitEnumConst(stmt);
                    return;
            }

            // statement bubbling statements
            int nodeLen = _nodes.Count;
            try
            {
                switch (stmt.Name)
                {
                    case "assignment":
                        _visitAssignment(stmt);
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
                                        if (sym.DataType.Classify() == TypeClassifier.POINTER)
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
                ErrorQueue.Add(se);

                while (_nodes.Count > nodeLen)
                    _nodes.RemoveAt(_nodes.Count - 1);

                _nodes.Add(new StatementNode("None"));
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

        private void _visitAssignment(ASTNode stmt)
        {
            var varTypes = new List<IDataType>();
            var exprTypes = new List<IDataType>();
            string op = "";

            foreach (var node in stmt.Content)
            {
                switch (node.Name)
                {
                    case "assign_var":
                        if (varTypes.Count == 0)
                            _nodes.Add(new ExprNode("AssignVars", new SimpleType()));

                        _visitAssignVar((ASTNode)node);
                        varTypes.Add(_nodes.Last().Type);

                        MergeBack();
                        break;
                    case "assign_op":
                        op = string.Join("", ((ASTNode)node).Content.Select(x => ((TokenNode)x).Tok.Type));
                        break;
                    case "expr":
                        if (exprTypes.Count == 0)
                            _nodes.Add(new ExprNode("AssignExprs", new SimpleType()));

                        _visitExpr((ASTNode)node);
                        exprTypes.Add(_nodes.Last().Type);

                        MergeBack();
                        break;
                }
            }

            string subOp = op.Length > 1 ? string.Join("", op.Take(op.Length - 1)) : "";

            if (varTypes.Count == exprTypes.Count)
            {
                for (int i = 0; i < varTypes.Count; i++)
                {
                    if (!varTypes[i].Coerce(exprTypes[i]))
                    {
                        if (subOp != "")
                        {
                            IDataType tt = varTypes[i];
                            CheckOperand(ref tt, exprTypes[i], subOp, stmt.Position);
                        }
                        else
                            throw new SemanticException("Unable to assign to dissimilar types", stmt.Position);
                    }
                }
            }
            else if (varTypes.Count < exprTypes.Count)
            {
                int metValues = 0;

                using (var e1 = varTypes.GetEnumerator())
                using (var e2 = exprTypes.GetEnumerator())
                {
                    while (e1.MoveNext() && e2.MoveNext())
                    {
                        if (e2.Current.Classify() == TypeClassifier.TUPLE)
                        {
                            foreach (var type in ((TupleType)e2.Current).Types)
                            {
                                if (e1.Current.Coerce(type))
                                {
                                    if (!e1.MoveNext()) break;
                                } 
                                else if (subOp != "")
                                {
                                    IDataType tt = e1.Current;
                                    CheckOperand(ref tt, type, subOp, stmt.Position);
                                }
                                else
                                    throw new SemanticException("Unable to assign to dissimilar types", stmt.Position);
                            }
                        }
                        else if (!e1.Current.Coerce(e2.Current))
                        {
                            if (subOp != "")
                            {
                                IDataType tt = e1.Current;
                                CheckOperand(ref tt, e2.Current, subOp, stmt.Position);
                            }
                            else
                                throw new SemanticException("Unable to assign to dissimilar types", stmt.Position);

                        }
                           
                        metValues++;
                    }
                }

                if (metValues < exprTypes.Count)
                    throw new SemanticException("Too many expressions for the given assignment", stmt.Position);
            }
            else
                throw new SemanticException("Too many expressions for the given assignment", stmt.Position);

            _nodes.Add(new StatementNode("Assignment"));
            PushForward(2);
        }

        private void _visitAssignVar(ASTNode assignVar)
        {
            foreach (var node in assignVar.Content)
            {
                if (node.Name == "TOKEN")
                {
                    var token = ((TokenNode)node).Tok;

                    if (token.Type == "IDENTIFIER")
                    {
                        if (token.Value == "_")
                            _nodes.Add(new IdentifierNode("_", new SimpleType(), false));
                        else if (_table.Lookup(token.Value, out Symbol symbol))
                        {
                            if (symbol.Modifiers.Contains(Modifier.CONSTEXPR) || symbol.Modifiers.Contains(Modifier.CONSTANT))
                                throw new SemanticException("Unable to assign to immutable value", node.Position);
                            else
                                _nodes.Add(new IdentifierNode(symbol.Name, symbol.DataType, false));
                        }
                        else
                            throw new SemanticException($"Undefined Identifier: `{token.Value}`", node.Position);
                    }
                    // this
                    else
                    {
                        if (_table.Lookup("$THIS", out Symbol instance))
                            _nodes.Add(new IdentifierNode("$THIS", instance.DataType, true));
                        else
                            throw new SemanticException("Use of `this` outside of property", node.Position);
                    }
                }
                else if (node.Name == "trailer")
                {
                    _visitTrailer((ASTNode)node);
                }
            }

            if (!Modifiable(_nodes.Last()))
                throw new SemanticException("Unable to assign to immutable value", assignVar.Position);
        }
    }
}
