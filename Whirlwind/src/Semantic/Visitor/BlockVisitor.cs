using Whirlwind.Parser;
using Whirlwind.Types;

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
                            _visitIf(blockStatement);
                            break;
                    }

                    break;
                case "func_stmt":
                    break;
                case "subscope":
                    break;
            }
        }

        private void _visitIf(ASTNode ifStmt)
        {

        }
    }
}
