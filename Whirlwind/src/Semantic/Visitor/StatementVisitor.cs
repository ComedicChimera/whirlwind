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
                        _visitDelete(stmt);
                        break;
                }
            }
            catch (SemanticException se) {
                ErrorQueue.Add(se);

                while (_nodes.Count > nodeLen)
                    _nodes.RemoveAt(_nodes.Count - 1);

                _nodes.Add(new StatementNode("None"));

                _clearPossibleContext();
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
                _nodes.Add(new StatementNode(stmtName));
            else if (exprCount == 1)
            {
                _nodes.Add(new StatementNode(stmtName));
                PushForward();
            }
            else
            {
                var tupleTypes = new List<DataType>();

                for (int i = exprCount - 1; i >= 0; i--)
                {
                    tupleTypes.Add(_nodes[_nodes.Count - (i + 1)].Type);
                }

                _nodes.Add(new ExprNode("Tuple", new TupleType(tupleTypes)));
                PushForward(exprCount);
                _nodes.Add(new StatementNode(stmtName));
                PushForward();
            }
        }

        private void _visitDelete(ASTNode stmt)
        {
            int deleteIdCount = 0;

            foreach (var item in stmt.Content)
            {
                if (item.Name == "delete_id")
                {
                    var deleteId = (ASTNode)item;                   

                    if (deleteId.Content.Count == 3)
                    {
                        if (!_isFinalizer)
                            throw new SemanticException("Unable to delete member variables outside of finalizer", deleteId.Content[0].Position);

                        // if we are in a finalizer a this pointer must exist
                        _table.Lookup("$THIS", out Symbol thisPtr);                       

                        if (thisPtr.DataType is StructType st)
                        {
                            var name = ((TokenNode)deleteId.Content[2]).Tok.Value;

                            if (st.Members.ContainsKey(name))
                            {
                                if (st.Members[name].DataType is PointerType pt)
                                {
                                    if (!_registrar.DeleteResource(pt.Owner))
                                        throw new SemanticException("Unable to delete resource from a non-owner", deleteId.Content[2].Position);

                                    _nodes.Add(new ExprNode("GetMember", pt));

                                    _nodes.Add(new IdentifierNode("$THIS", thisPtr.DataType));
                                    _nodes.Add(new IdentifierNode(name, pt));
                                    MergeBack();
                                }
                                else
                                    throw new SemanticException("Unable to a delete from a non-pointer", deleteId.Content[2].Position);
                            }
                            else
                                throw new SemanticException($"Struct contains no member by name `{name}`", deleteId.Content[2].Position);
                        }
                        else
                            throw new SemanticException("Unable to delete member of a non-owning type", deleteId.Content[0].Position);
                    }
                    else
                    {
                        var name = ((TokenNode)deleteId.Content[0]).Tok.Value;
                        var scope = _table.GetScope();

                        if (!scope.Select(x => x.Name).Contains(name))
                            throw new SemanticException("Unable to delete from owner that either does not exist or is not in the current scope",
                                deleteId.Content[0].Position);

                        if (scope.Where(x => name == x.Name).First().DataType is PointerType pt)
                        {
                            if (_registrar.DeleteResource(pt.Owner))
                                _nodes.Add(new IdentifierNode(name, pt));
                            else
                                throw new SemanticException("Unable to delete resource from a non-owner", deleteId.Content[2].Position);
                        }
                        else
                            throw new SemanticException("Unable to delete from a non-pointer", deleteId.Content[2].Position);                      
                    }

                    deleteIdCount++;
                }
            }

            _nodes.Add(new StatementNode("Delete"));
            PushForward(deleteIdCount);
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
                            _nodes.Add(new ExprNode("AssignVars", new VoidType()));

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
                            _nodes.Add(new ExprNode("AssignExprs", new VoidType()));

                        {
                            var exprNode = (ASTNode)node;

                            _addContext(exprNode);
                            _visitExpr(exprNode);
                            _clearPossibleContext();
                        }
                        
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
                    if (exprTypes[i] is IncompleteType)
                        _inferLambdaAssignContext(varTypes[i], exprTypes, i);

                    if (subOp != "")
                    {
                        DataType ot = varTypes[i].ConstCopy();
                        // negate constancy from const copy
                        ot.Constant = varTypes[i].Constant;
                        CheckOperand(ref ot, exprTypes[i], subOp, stmt.Position);

                        if (!varTypes[i].Coerce(ot))
                            throw new SemanticException("Unable to apply operator which would result in type change", stmt.Position);
                    }
                    else if (!varTypes[i].Coerce(exprTypes[i]))
                        throw new SemanticException("Unable to assign to dissimilar types", stmt.Position);
                }
            }
            else if (varTypes.Count > exprTypes.Count)
            {
                int i = 0, j = 0;
                DataType varType, exprType;

                while (i < varTypes.Count && j < exprTypes.Count)
                {
                    varType = varTypes[i];
                    exprType = exprTypes[j];

                    if (exprType is IncompleteType)
                        _inferLambdaAssignContext(varType, exprTypes, j);

                    if (exprType.Classify() == TypeClassifier.TUPLE)
                    {
                        var tupleTypes = ((TupleType)exprType).Types;

                        for (int k = 0; k < tupleTypes.Count; k++)
                        {
                            var type = tupleTypes[k];

                            if (type is IncompleteType)
                                _inferLambdaAssignContext(varType, exprTypes, k);

                            if (subOp != "")
                            {
                                // b/c we are just checking what operators are valid constancy does not matter
                                DataType ot = varType.ConstCopy();
                                CheckOperand(ref ot, type, subOp, stmt.Position);

                                if (!varType.Coerce(ot))
                                    throw new SemanticException("Unable to apply operator which would result in type change", stmt.Position);
                            }
                            else if (varType.Coerce(type))
                            {
                                i++;

                                if (i < varTypes.Count)
                                    varType = varTypes[i];
                                else
                                    break;
                            }
                            else
                                throw new SemanticException("Unable to assign to dissimilar types", stmt.Position);
                        }
                    }
                    else if (subOp != "")
                    {
                        // b/c we are just checking what operators are valid constancy does not matter
                        DataType ot = varType.ConstCopy();
                        CheckOperand(ref ot, exprType, subOp, stmt.Position);

                        if (!varType.Coerce(ot))
                            throw new SemanticException("Unable to apply operator which would result in type change", stmt.Position);
                    }
                    else if (!varType.Coerce(exprType))
                        throw new SemanticException("Unable to assign to dissimilar types", stmt.Position);

                    i++;
                    j++;
                }

                if (j < exprTypes.Count)
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
                            if (symbol.Modifiers.Contains(Modifier.CONSTEXPR) || symbol.DataType.Constant)
                                throw new SemanticException("Unable to assign to immutable value", node.Position);
                            else
                                _nodes.Add(new IdentifierNode(symbol.Name, symbol.DataType));
                        }
                        else
                            throw new SemanticException($"Undefined Identifier: `{token.Value}`", node.Position);
                    }
                    else if (token.Type == "_")
                        _nodes.Add(new IdentifierNode("_", new VoidType()));
                    else if (token.Type == "*")
                        derefCount++;
                    // this
                    else
                    {
                        if (_table.Lookup("$THIS", out Symbol instance))
                            _nodes.Add(new IdentifierNode("$THIS", instance.DataType));
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
                        if (_isVoid(pType.DataType))
                            throw new SemanticException("Unable to dereference void pointer", assignVar.Position);

                        _nodes.Add(new ExprNode("Dereference", pType.DataType));
                    }
                    else
                        _nodes.Add(new ExprNode("Dereference", new PointerType(pType.DataType, pType.Pointers - derefCount)));

                    PushForward();
                }
                else
                    throw new SemanticException("Unable to dereference non-pointer", assignVar.Position);
            }
        }

        private void _inferLambdaAssignContext(DataType pctx, List<DataType> exprTypes, int pos)
        {
            _giveContext((IncompleteNode)(((ExprNode)_nodes.Last()).Nodes[pos]), pctx);

            ((ExprNode)_nodes[_nodes.Count - 2]).Nodes[pos] = _nodes.Last();
            exprTypes[pos] = _nodes.Last().Type;

            _nodes.RemoveAt(_nodes.Count - 1);
        }
    }
}
