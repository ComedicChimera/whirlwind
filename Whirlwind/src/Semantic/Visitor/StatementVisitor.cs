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
                        _isExprStmt = true;
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

                _clearContext();
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
                    bool hasThis = false;
                    
                    foreach (var elem in ((ASTNode)item).Content)
                    {
                        if (elem is TokenNode tkNode)
                        {
                            if (tkNode.Tok.Type == "THIS")
                            {
                                if (!_isFinalizer)
                                    throw new SemanticException("Unable to delete member variables outside of finalizer", elem.Position);

                                // if we are in a finalizer a this pointer must exist
                                _table.Lookup("$THIS", out Symbol thisPtr);

                                if (thisPtr.DataType.Classify() != TypeClassifier.STRUCT_INSTANCE)
                                    throw new SemanticException("Unable to delete member of anything other than a struct", elem.Position);

                                _nodes.Add(new IdentifierNode("$THIS", thisPtr.DataType));
                                hasThis = true;
                            }
                            else if (tkNode.Tok.Type == "IDENTIFIER")
                            {
                                var name = tkNode.Tok.Value;

                                if (hasThis)
                                {
                                    var st = (StructType)_nodes.Last().Type;

                                    if (st.Members.ContainsKey(name))
                                    {
                                        var smdt = st.Members[name].DataType;

                                        _nodes.Add(new IdentifierNode(name, smdt));

                                        _nodes.Add(new ExprNode("GetMember", smdt));
                                        PushForward(2);
                                    }
                                    else
                                        throw new SemanticException($"Struct contains no member by name `{name}`", elem.Position);
                                }
                                else if (_table.Lookup(name, out Symbol symbol))
                                    _nodes.Add(new IdentifierNode(name, symbol.DataType));
                                else
                                    throw new SemanticException($"Undefined symbol: `{name}`", elem.Position);
                            }
                        }
                        // can only be trailer
                        else
                        {
                            var trailerNode = (ASTNode)elem;

                            if (trailerNode.Content.First() is TokenNode trailerFirst && trailerFirst.Tok.Type == "[")
                                _visitSubscript(_nodes.Last().Type, trailerNode);
                            else
                                throw new SemanticException("Unable to perform the given operation in a delete statement", trailerNode.Position);
                        }
                    }

                    if (!(_nodes.Last().Type is PointerType pt && pt.IsDynamicPointer))
                        throw new SemanticException("Only able to delete dynamic pointers", item.Position);

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
                            _clearContext();
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

                    var varType = varTypes[i].ConstCopy();
                    // negate constancy from const copy
                    varType.Constant = varTypes[i].Constant;

                    if (subOp != "")
                    {                      
                        CheckOperand(ref varType, exprTypes[i], subOp, stmt.Position);

                        if (!varTypes[i].Coerce(varType))
                            throw new SemanticException("Unable to apply operator which would result in type change", stmt.Position);
                    }
                    else if (!varType.Coerce(exprTypes[i]))
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
            _isSetContext = true;
            int derefCount = 0;

            foreach (var node in assignVar.Content)
            {
                // check the get operator overload for all of the nodes
                // the use set overload where it is not the terminating
                // operation to as to ensure that when we need pass a
                // compiler generated instance to the set operator accessed
                // from the get operator, nothing goes down in flames
                if (_nodes.Last().Name.EndsWith("SetOverload"))
                {
                    var setNode = (ExprNode)_nodes.Last();
                    var baseType = setNode.Nodes.First().Type;

                    if (HasOverload(baseType, setNode.Name.StartsWith("Subscript") ? "__[]__" : "__[:]__",
                        new ArgumentList(setNode.Nodes.Skip(1).Select(x => x.Type).ToList()), out DataType returnType))
                    {
                        if (!setNode.Type.Coerce(returnType))
                            throw new SemanticException("Unable to use set overload with an incompatable get overload", assignVar.Position);
                    }
                    else
                        throw new SemanticException("In order to utilize set overload in this context, a get overload must also be defined", 
                            assignVar.Position);
                }

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
                if (_nodes.Last().Type is PointerType pType)
                {
                    if (_isVoid(pType.DataType))
                        throw new SemanticException("Unable to dereference void pointer", assignVar.Position);
                    else
                    _nodes.Add(new ExprNode("Dereference", pType.DataType));

                PushForward();
                }
                else
                    throw new SemanticException("Unable to dereference non-pointer", assignVar.Position);
            }

            _isSetContext = false;
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
