using Whirlwind.Parser;
using Whirlwind.Types;

using static Whirlwind.Semantic.Checker.Checker;

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

            if (stmt.Name == "variable_decl")
            {
                // block bubbling statements
                _visitVarDecl(stmt, new List<Modifier>());
                return;
            }

            // statement bubbling statements
            int nodeLen = _nodes.Count;
            try
            {
                switch (stmt.Name)
                {
                    case "assignment":
                        _isGetMode = true;

                        _visitAssignment(stmt);

                        _isGetMode = false;
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
                            throw new SemanticException("Invalid context for a yield statement", stmt.Position);
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
                var tupleTypes = new List<DataType>();

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

        private void _visitAssignment(ASTNode stmt)
        {
            var varTypes = new List<DataType>();
            var exprTypes = new List<DataType>();
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
                        {
                            var opNode = (ASTNode)node;

                            if (opNode.Content.Count == 2)
                            {
                                op += ((ASTNode)opNode.Content[0]).Content
                                    .Select(x => ((TokenNode)x).Tok.Type)
                                    .Aggregate((a, b) => a + b);
                            }

                            op += "=";
                        }
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
                            DataType tt = varTypes[i];
                            CheckOperand(ref tt, exprTypes[i], subOp, stmt.Position);

                            if (!varTypes[i].Coerce(tt))
                                throw new SemanticException("Unable to assign to dissimilar types", stmt.Position);
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
                                    DataType tt = e1.Current;
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
                                DataType tt = e1.Current;
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
            int derefCount = 0;

            foreach (var node in assignVar.Content)
            {
                if (node.Name == "TOKEN")
                {
                    var token = ((TokenNode)node).Tok;

                    if (token.Type == "IDENTIFIER")
                    {
                        if (_table.Lookup(token.Value, out Symbol symbol))
                        {
                            if (symbol.Modifiers.Contains(Modifier.CONSTEXPR) || symbol.Modifiers.Contains(Modifier.CONSTANT))
                                throw new SemanticException("Unable to assign to immutable value", node.Position);
                            else
                                _nodes.Add(new IdentifierNode(symbol.Name, symbol.DataType, false));
                        }
                        else
                            throw new SemanticException($"Undefined Identifier: `{token.Value}`", node.Position);
                    }
                    else if (token.Type == "_")
                        _nodes.Add(new IdentifierNode("_", new SimpleType(), false));
                    else if (token.Type == "*")
                        derefCount++;
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
                    _visitTrailer((ASTNode)node);
            }

            if (!Modifiable(_nodes.Last()))
                throw new SemanticException("Unable to assign to immutable value", assignVar.Position);

            if (derefCount > 0)
            {
                if (_nodes.Last().Type.Classify() == TypeClassifier.POINTER)
                {
                    PointerType pType = (PointerType)_nodes.Last().Type;

                    if (pType.Pointers < derefCount)
                        throw new SemanticException("Unable to dereference non-pointer", assignVar.Position);
                    else if (pType.Pointers == derefCount)
                    {
                        if (_isVoid(pType.Type))
                            throw new SemanticException("Unable to dereference void pointer", assignVar.Position);

                        _nodes.Add(new ExprNode("Dereference", pType.Type));
                    }
                    else
                        _nodes.Add(new ExprNode("Dereference", new PointerType(pType.Type, pType.Pointers - derefCount)));

                    PushForward();
                }
                else
                    throw new SemanticException("Unable to dereference non-pointer", assignVar.Position);
            }
        }
    }
}
