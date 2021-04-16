package validate

import (
	"whirlwind/common"
	"whirlwind/typing"
)

// coerceTo performs a coercion check from the type of HIRExpr to the given type
func (w *Walker) coerceTo(expr common.HIRExpr, dest typing.DataType) bool {
	return w.solver.CoerceTo(expr.Type(), dest)
}

// typeListFromExprs extracts a type list from a list of expressions
func typeListFromExprs(exprs []common.HIRExpr) []typing.DataType {
	tl := make([]typing.DataType, len(exprs))
	for i, expr := range exprs {
		tl[i] = expr.Type()
	}

	return tl
}
