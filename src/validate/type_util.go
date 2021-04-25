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

// definiteInnerType extracts the inner type of a data type and then checks to
// see if it unknown or not.  This is used for operators that cannot accept
// unknown types.
func definiteInnerType(dt typing.DataType) (typing.DataType, bool) {
	it := typing.InnerType(dt)

	_, ok := it.(*typing.UnknownType)
	return it, ok
}

// typeListFromExprs extracts a type list from a list of expressions
func typeListFromExprs(exprs []common.HIRExpr) []typing.DataType {
	tl := make([]typing.DataType, len(exprs))
	for i, expr := range exprs {
		tl[i] = expr.Type()
	}

	return tl
}
