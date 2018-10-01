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
            ASTNode stmt = (ASTNode)node.Content[0];

            switch (stmt.Name)
            {
                case "stmt":
                    _visitStatement(stmt, context);
                    break;
                case "block_stmt":
                    ASTNode blockStatement = (ASTNode)stmt.Content[0];

                    switch (blockStatement.Name)
                    {
                        case "if_stmt":
                            _visitIf(blockStatement, context);
                            break;
                        case "select_stmt":
                            _visitSelect(blockStatement, context);
                            break;
                    }

                    break;
                case "func_stmt":
                    break;
                case "subscope":
                    break;
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

                        if (!new SimpleType(SimpleType.DataType.BOOL).Coerce(_nodes.Last().Type))
                            throw new SemanticException("Condition value of if statement must be a boolean", 
                                ((ASTNode)item).Content[2].Position);

                        MergeBack();
                        break;
                    case "block":
                        _visitBlock((ASTNode)item, context);
                        break;
                    case "elif_stmt":
                        if (!compound)
                        {
                            _nodes.Add(new BlockNode("CompoundIf"));
                            PushToBlock();

                            compound = true;
                        }

                        _nodes.Add(new BlockNode("Elif"));

                        _visitExpr((ASTNode)((ASTNode)item).Content[2]);

                        if (!new SimpleType(SimpleType.DataType.BOOL).Coerce(_nodes.Last().Type))
                            throw new SemanticException("Condition value of elif statement must be a boolean", 
                                ((ASTNode)item).Content[2].Position);

                        PushForward();

                        _visitBlock((ASTNode)((ASTNode)item).Content[4], context);
                        MergeToBlock();
                        break;
                    case "else_stmt":
                        if (!compound)
                        {
                            _nodes.Add(new BlockNode("CompoundIf"));
                            PushToBlock();

                            compound = true;
                        }

                        _nodes.Add(new BlockNode("Else"));
                        _visitBlock((ASTNode)((ASTNode)item).Content[1], context);
                        break;
                }
            }
        }

        private void _visitSelect(ASTNode blockStmt, StatementContext context)
        {
            _nodes.Add(new BlockNode("Select"));
            context.BreakValid = true;

            _visitExpr((ASTNode)blockStmt.Content[2]);
            MergeBack();

            foreach (var item in ((ASTNode)blockStmt.Content[5]).Content)
            {
                if (item.Name == "case")
                {
                    _nodes.Add(new BlockNode("Case"));

                    foreach (var caseItem in ((ASTNode)item).Content)
                    {
                        if (caseItem.Name == "expr")
                        {
                            _visitExpr((ASTNode)caseItem);
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

                MergeToBlock();
            }
        }
    }
}
