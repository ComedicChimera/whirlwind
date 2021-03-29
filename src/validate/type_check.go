package validate

import (
	"whirlwind/common"
	"whirlwind/typing"
)

// coerceTo performs a coercion check from the type of HIRExpr to the given type
func (w *Walker) coerceTo(expr common.HIRExpr, dest typing.DataType) bool {
	return w.solver.CoerceTo(expr.Type(), dest)
}
