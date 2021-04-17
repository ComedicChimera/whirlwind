package validate

import (
	"whirlwind/common"
	"whirlwind/typing"
)

// coerceTo performs a coercion check from the type of HIRExpr to the given type
func (w *Walker) coerceTo(expr common.HIRExpr, dest typing.DataType) bool {
	return w.solver.CoerceTo(expr.Type(), dest)
}

// getBindings loads all the bindings for a given data type
func (w *Walker) getBindings(dt typing.DataType) []*typing.InterfType {
	return append(w.solver.GetBindings(w.solver.GlobalBindings, dt), w.solver.GetBindings(w.solver.LocalBindings, dt)...)
}

// innerType accesses the interior type of a data type.  It acts as a wrapper
// around `typing.InnerType` with one key difference -- this function simply
// returns false if the unknown type can't be extracted.  This function should
// only be used inside of expressions.
func innerType(dt typing.DataType) (typing.DataType, bool) {
	if uk, ok := dt.(*typing.UnknownType); ok {
		if uk.EvalType == nil {
			return nil, false
		} else {
			return uk.EvalType, true
		}
	}

	return typing.InnerType(dt), true
}

// typeListFromExprs extracts a type list from a list of expressions
func typeListFromExprs(exprs []common.HIRExpr) []typing.DataType {
	tl := make([]typing.DataType, len(exprs))
	for i, expr := range exprs {
		tl[i] = expr.Type()
	}

	return tl
}
