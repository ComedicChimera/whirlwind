using System.Linq;
using System.Collections.Generic;

using static Whirlwind.Semantic.Checker.Checker;

using Whirlwind.Syntax;
using Whirlwind.Types;

namespace Whirlwind.Semantic.Visitor
{
    partial class Visitor
    {
        private void _visitBlock(ASTNode node, StatementContext context)
        {
            foreach (var subNode in node.Content)
            {
                var stmt = (ASTNode)subNode;

                switch (stmt.Name)
                {
                    case "stmt":
                        _visitStatement(stmt, context);
                        break;
                    case "block_stmt":
                        _table.AddScope();
                        _table.DescendScope();

                        ASTNode blockStatement = (ASTNode)stmt.Content[0];

                        switch (blockStatement.Name)
                        {
                            case "if_stmt":
                                _visitIf(blockStatement, context);
                                break;
                            case "select_stmt":
                                _visitSelect(blockStatement, context);
                                break;
                            case "for_loop":
                                _visitForLoop(blockStatement, context);
                                break;
                        }

                        _table.AscendScope();

                        break;
                    case "func_decl":
                        _visitFunction(stmt, new List<Modifier>());

                        if (((BlockNode)_nodes.Last()).Block.Count == 1)
                        {
                            var fnType = (FunctionType)((BlockNode)_nodes.Last()).Nodes[0].Type;
                            int scopeDepth = _table.GetScopeDepth();

                            var returnCtx = _returnContext;
                            _returnContext = fnType.ReturnType;

                            try
                            {
                                _visitFunctionBody(
                                    ((IncompleteNode)((BlockNode)_nodes.Last()).Block[0]).AST,
                                    fnType
                                );

                                ((BlockNode)_nodes.Last()).Block.RemoveAt(0);
                            } 
                            catch (SemanticException smex)
                            {
                                smex.FileName = _fileName;
                                ErrorQueue.Add(smex);

                                ((BlockNode)_nodes.Last()).Block = new List<ITypeNode>();

                                if (scopeDepth != _table.GetScopeDepth())
                                    _table.AscendScope();

                                _clearContext();
                            }
                            finally
                            {
                                _returnContext = returnCtx;
                            }
                            
                        }
                        break;
                    case "subscope":
                        _table.AddScope();
                        _table.DescendScope();

                        _nodes.Add(new BlockNode("Subscope"));

                        _visitBlockNode(stmt, context);

                        _table.AscendScope();
                        break;
                }

                MergeToBlock();
            }
        }

        private void _visitIf(ASTNode ifStmt, StatementContext context)
        {
            bool compound = false;

            _nodes.Add(new BlockNode("If"));

            foreach (var item in ifStmt.Content)
            {
                switch (item.Name)
                {
                    case "expr":                       
                        _visitExpr((ASTNode)item);

                        if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                            throw new SemanticException("Condition value of if statement must be a boolean", item.Position);

                        MergeBack();
                        break;
                    case "variable_decl":
                        _visitVarDecl((ASTNode)item, new List<Modifier>());
                        MergeBack();
                        break;
                    case "block":
                        _visitBlockNode((ASTNode)item, context);
                        break;
                    case "elif_block":
                        {
                            _table.AscendScope();

                            _table.AddScope();
                            _table.DescendScope();

                            if (!compound)
                            {
                                _nodes.Add(new BlockNode("CompoundIf"));
                                PushToBlock();

                                compound = true;
                            }

                            _nodes.Add(new BlockNode("Elif"));

                            ASTNode decl = (ASTNode)item;

                            int exprNdx = 2;
                            if (decl.Content.Count > 5)
                            {
                                exprNdx = 4;

                                _visitVarDecl((ASTNode)decl.Content[2], new List<Modifier>());
                                MergeBack();
                            }

                            _visitExpr((ASTNode)((ASTNode)item).Content[exprNdx]);

                            if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                                throw new SemanticException("Condition value of elif statement must be a boolean",
                                    ((ASTNode)item).Content[exprNdx].Position);

                            MergeBack();

                            _visitBlockNode((ASTNode)((ASTNode)item).Content[exprNdx + 2], context);
                            MergeToBlock();
                        }
                        break;
                    case "else_block":
                        _table.AscendScope();

                        _table.AddScope();
                        _table.DescendScope();

                        if (!compound)
                        {
                            _nodes.Add(new BlockNode("CompoundIf"));
                            PushToBlock();

                            compound = true;
                        }

                        _nodes.Add(new BlockNode("Else"));
                        _visitBlockNode((ASTNode)((ASTNode)item).Content[1], context);

                        MergeToBlock();
                        break;
                }
            }
        }

        private void _visitSelect(ASTNode blockStmt, StatementContext context)
        {
            _nodes.Add(new BlockNode("Select"));

            _visitExpr((ASTNode)blockStmt.Content[2]);
            DataType exprType = _nodes.Last().Type;

            if (!Comparable(exprType, willDecompose: true))
                throw new SemanticException("Unable to select over type of " + exprType.ToString(), blockStmt.Content[2].Position);

            MergeBack();

            foreach (var item in ((ASTNode)blockStmt.Content[5]).Content)
            {
                _table.AddScope();
                _table.DescendScope();

                if (item.Name == "case")
                {
                    _nodes.Add(new BlockNode("Case"));

                    var caseNode = (ASTNode)item;

                    foreach (var caseItem in caseNode.Content)
                    {
                        if (caseItem.Name == "case_expr")
                        {
                            if (!_visitCaseExpr((ASTNode)caseItem, exprType, caseNode.Content.Count < 5))
                                throw new SemanticException("The case expressions must be the same type of the select root", caseItem.Position);

                            MergeBack();
                        }
                        else if (caseItem.Name == "main")
                        {
                            _visitBlock((ASTNode)caseItem, context);
                        }
                    }

                    MergeToBlock();
                }
                else
                {
                    _nodes.Add(new BlockNode("Default"));

                    _visitBlock((ASTNode)((ASTNode)item).Content[2], context);

                    MergeToBlock();
                }

                _table.AscendScope();
            }
        }

        private void _visitForLoop(ASTNode node, StatementContext context)
        {
            context.BreakValid = true;
            context.ContinueValid = true;

            if (node.Content.Where(x => x.Name != "after_clause").Count() == 2)
            {
                _nodes.Add(new BlockNode("ForInfinite"));

                _visitBlockNode((ASTNode)node.Content[1], context);

                return;
            }
            else
            {
                var body = (ASTNode)node.Content[1];

                foreach (var subNode in body.Content)
                {
                    if (subNode.Name == "for_expr")
                    {
                        _nodes.Add(new BlockNode("ForCondition"));

                        foreach (var elem in ((ASTNode)subNode).Content)
                        {
                            if (elem.Name == "expr")
                            {
                                _visitExpr((ASTNode)elem);

                                if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                                    throw new SemanticException("Condition of for loop must be a boolean", subNode.Position);

                                MergeBack();
                            }
                            else if (elem.Name == "variable_decl")
                            {
                                _visitVarDecl((ASTNode)elem, new List<Modifier>());
                                MergeBack();
                            }
                        }                       
                    }
                    else if (subNode.Name == "iterator")
                    {
                        _nodes.Add(new BlockNode("ForIter"));

                        _visitIterator((ASTNode)subNode);

                        MergeBack();
                    }
                    else if (subNode.Name == "c_for")
                    {
                        _visitCFor((ASTNode)subNode);

                        if (((ExprNode)_nodes.Last()).Nodes.Count > 0)
                        {
                            _nodes.Add(new BlockNode("CFor"));
                            PushForward();
                        }
                        else
                        {
                            _nodes.RemoveAt(_nodes.Count - 1);
                            _nodes.Add(new BlockNode("ForInfinite"));
                        }
                    }
                }

                if (node.Content.Last().Name == "after_clause")
                {
                    _visitBlockNode((ASTNode)node.Content[node.Content.Count - 2], context);
                    _visitAfterClause((ASTNode)node.Content.Last(), context);
                }
                else
                    _visitBlockNode((ASTNode)node.Content.Last(), context);
            }
        }

        private void _visitCFor(ASTNode node)
        {
            int componentPosition = 0;
            string iterVarName = "";
            Token token;

            _nodes.Add(new ExprNode("CForExpr", new NoneType()));

            foreach (var item in node.Content)
            {
                if (item.Name == "TOKEN")
                {
                    token = ((TokenNode)item).Tok;

                    switch (token.Type)
                    {
                        case ";":
                            componentPosition++;
                            break;
                        case "IDENTIFIER":
                            iterVarName = token.Value;

                            if (iterVarName == "_")
                                throw new SemanticException("Unable to declare variable as ignored value", item.Position);

                            break;
                    }
                }
                else if (item.Name == "expr")
                {
                    _visitExpr((ASTNode)item);

                    switch (componentPosition)
                    {
                        case 0:
                            _nodes.Add(new IdentifierNode(iterVarName, _nodes.Last().Type.LValueCopy()));
                            _nodes.Add(new ExprNode("IterVarDecl", _nodes.Last().Type));
                            PushForward(2);

                            ((ExprNode)_nodes.Last()).Nodes.Reverse();

                            // first symbol defined a new scope so no need to check
                            _table.AddSymbol(new Symbol(iterVarName, _nodes.Last().Type));

                            MergeBack();
                            break;
                        case 1:
                            if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                                throw new SemanticException("Condition of for loop must be a boolean", item.Position);

                            _nodes.Add(new ExprNode("CForCondition", _nodes.Last().Type));
                            PushForward();

                            MergeBack();
                            break;
                        case 2:
                            _nodes.Add(new ExprNode("CForUpdateExpr", _nodes.Last().Type));
                            PushForward();

                            MergeBack();
                            break;
                    }
                }
                else if (item.Name == "assignment")
                {
                    _visitAssignment((ASTNode)item);
                    _nodes.Add(new ExprNode("CForUpdateAssignment", new NoneType()));
                    PushForward();

                    MergeBack();
                }
            }
        }

        private void _visitAfterClause(ASTNode afterBlock, StatementContext context)
        {
            _nodes.Add(new BlockNode("After"));

            _visitBlockNode((ASTNode)afterBlock.Content[1], context);

            MergeToBlock();
        }

        private void _visitBlockNode(ASTNode block, StatementContext context)
        {
            if (block.Content.Count == 1)
            {
                _visitStatement((ASTNode)block.Content[0], context);
                MergeToBlock();
            }
            else if (block.Content.Count == 3)
                _visitBlock((ASTNode)block.Content[1], context);
        }
    }
}
