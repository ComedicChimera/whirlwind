package validate

import (
	"whirlwind/common"
	"whirlwind/syntax"
)

// walkExpr walks an `expr` node
func (w *Walker) walkExpr(expr *syntax.ASTBranch) (common.HIRExpr, bool) {
	return nil, false
}
