using Whirlwind.Parser;
using Whirlwind.Types;

using System.Linq;
using System.Collections.Generic;

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
                            case "from_block":
                                _visitFromBlock(blockStatement, context);
                                break;
                        }

                        _table.AscendScope();

                        break;
                    case "func_decl":
                        _visitFunction(stmt, new List<Modifier>());

                        if (((BlockNode)_nodes.Last()).Block.Count == 1)
                        {
                            _visitFunctionBody(
                                ((IncompleteNode)((BlockNode)_nodes.Last()).Block[0]).AST, 
                                (FunctionType)((BlockNode)_nodes.Last()).Nodes[0].Type
                            );

                            ((BlockNode)_nodes.Last()).Block.RemoveAt(0);
                        }
                        break;
                    case "subscope":
                        _nodes.Add(new BlockNode("Subscope"));

                        _visitBlockNode(stmt, context);
                        break;
                }

                MergeToBlock();
            }
        }

        private void _visitIf(ASTNode ifStmt, StatementContext context)
        {
            bool compound = false;

            foreach (var item in ifStmt.Content)
            {
                switch (item.Name)
                {
                    case "expr":
                        _nodes.Add(new BlockNode("If"));
                        _visitExpr((ASTNode)item);

                        if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                            throw new SemanticException("Condition value of if statement must be a boolean", item.Position);

                        MergeBack();
                        break;
                    case "block":
                        _visitBlockNode((ASTNode)item, context);
                        break;
                    case "elif_block":
                        if (!compound)
                        {
                            _nodes.Add(new BlockNode("CompoundIf"));
                            PushToBlock();

                            compound = true;
                        }

                        _nodes.Add(new BlockNode("Elif"));

                        _visitExpr((ASTNode)((ASTNode)item).Content[2]);

                        if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                            throw new SemanticException("Condition value of elif statement must be a boolean",
                                ((ASTNode)item).Content[2].Position);

                        MergeBack();

                        _visitBlockNode((ASTNode)((ASTNode)item).Content[4], context);
                        MergeToBlock();
                        break;
                    case "else_block":
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

            MergeBack();

            foreach (var item in ((ASTNode)blockStmt.Content[5]).Content)
            {
                _table.AddScope();
                _table.DescendScope();

                if (item.Name == "case")
                {
                    _nodes.Add(new BlockNode("Case"));

                    foreach (var caseItem in ((ASTNode)item).Content)
                    {
                        if (caseItem.Name == "expr")
                        {
                            _visitExpr((ASTNode)caseItem);

                            if (!exprType.Coerce(_nodes.Last().Type))
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

            if (node.Content.Count == 2)
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
                    if (subNode.Name == "expr")
                    {
                        _nodes.Add(new BlockNode("ForCondition"));
                        _visitExpr((ASTNode)subNode);

                        if (!new SimpleType(SimpleType.SimpleClassifier.BOOL).Coerce(_nodes.Last().Type))
                            throw new SemanticException("Condition of for loop must be a boolean", subNode.Position);

                        MergeBack();
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

            _nodes.Add(new ExprNode("CForExpr", new SimpleType()));

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
                            _nodes.Add(new IdentifierNode(iterVarName, _nodes.Last().Type, false));
                            _nodes.Add(new ExprNode("IterVarDecl", _nodes.Last().Type));
                            PushForward(2);

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
                    _nodes.Add(new ExprNode("CForUpdateAssignment", new SimpleType()));
                    PushForward();

                    MergeBack();
                }
            }
        }

        private void _visitFromBlock(ASTNode blockStmt, StatementContext context)
        {
            foreach (var item in blockStmt.Content)
            {
                switch (item.Name)
                {
                    case "from_body":
                        _nodes.Add(new BlockNode("FromBlock"));

                        _table.AddScope();
                        _table.DescendScope();

                        _visitVarDecl((ASTNode)((ASTNode)item).Content[1], new List<Modifier>());
                        MergeBack();

                        _visitBlockNode((ASTNode)((ASTNode)item).Content[3], context);

                        _table.AscendScope();
                        break;
                    case "from_except_clause":
                        _nodes.Add(new BlockNode("FromExcept"));

                        context.ContinueValid = true;
                        _visitBlockNode((ASTNode)((ASTNode)item).Content[1], context);

                        MergeBack();
                        break;
                    case "after_clause":
                        _visitAfterClause((ASTNode)item, context);
                        break;
                }
            }
        }

        private void _visitAfterClause(ASTNode afterBlock, StatementContext context)
        {
            _nodes.Add(new BlockNode("After"));

            _visitBlockNode((ASTNode)afterBlock.Content[1], context);

            MergeBack();
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
