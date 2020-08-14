package analysis

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// walkExprList walks an `expr_list` node
func (w *Walker) walkExprList(branch *syntax.ASTBranch) ([]common.HIRExpr, bool) {
	exprs := make([]common.HIRExpr, branch.Len()/2+1)

	for i, node := range branch.Content {
		if subbranch, ok := node.(*syntax.ASTBranch); ok {
			if expr, ok := w.walkExpr(subbranch); ok {
				exprs[i/2] = expr
			} else {
				return nil, false
			}
		}
	}

	return exprs, true
}

// walkExpr walks an `expr` node
func (w *Walker) walkExpr(branch *syntax.ASTBranch) (common.HIRExpr, bool) {
	return nil, false
}
