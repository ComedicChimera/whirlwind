package validate

import (
	"whirlwind/common"
	"whirlwind/syntax"
)

// walkExprList walks an `expr_list` node
func (w *Walker) walkExprList(exprList *syntax.ASTBranch) ([]common.HIRExpr, bool) {
	return nil, false
}

// walkExpr walks an `expr` node
func (w *Walker) walkExpr(expr *syntax.ASTBranch) (common.HIRExpr, bool) {
	return nil, false
}
