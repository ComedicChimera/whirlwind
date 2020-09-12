package analysis

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"

	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// nothingType is a constant representing the `nothing` type (ie. void)
var nothingType types.DataType = types.PrimitiveType(types.PrimNothing)

// builtinTypeTable stores all of the globally defined built-in types
var builtinTypeTable = make(map[string]types.DataType)

// getBuiltin gets a built-in type from the built-ins table or attempts to
// load a new entry from the global table.  If this fails, then it throws a
// non-fatal error and returns false.
func (w *Walker) getBuiltin(name string, pos *util.TextPosition) (types.DataType, bool) {
	if bt, ok := builtinTypeTable[name]; ok {
		return bt, true
	}

	if sym := w.Lookup(name); sym != nil {
		return sym.Type, true
	}

	util.ThrowError(
		fmt.Sprintf("Built-in type symbol `%s` used but not defined", name),
		"Usage",
		pos,
	)

	return nil, false
}

// checkAssign ensures that a given assignment is valid
func checkAssign(lhs common.HIRExpr, rdt types.DataType, lpos, rpos *util.TextPosition) bool {
	if lhs.Category() == common.LValue && !lhs.Constant() {
		if rt, ok := isReference(lhs.Type()); ok {
			if rt.Owned {
				util.LogMod.LogError(util.NewWhirlErrorWithSuggestion(
					"Unable to assign to an owned reference",
					"Memory",
					"Try using the `->` operator or assigning to an inspected copy",
					lpos,
				))

				return false
			}
		}

		if !types.CoerceTo(rdt, lhs.Type()) {
			ThrowCoercionError(rdt, lhs.Type(), rpos)
			return false
		}

		return true
	}

	util.ThrowError(
		"Unable to assign to an immutable value",
		"Mutability",
		lpos,
	)

	return false
}

// checkReturn ensures that a given value can be returned.  `rtlocal` indicates
// whether or not the value being checked was created locally. NOTE: does not
// check that the rtExpr matches the return type
func (w *Walker) checkReturn(rtexpr common.HIRExpr, rtlocal bool, rtpos *util.TextPosition) bool {
	if rtlocal {
		for _, ref := range getContainedReferences(rtexpr.Type()) {
			if !ref.Owned && ref.Local {
				util.ThrowError(
					"Unable to return item that is or contains a free local reference",
					"Memory",
					rtpos,
				)

				return false
			}
		}
	}

	return true
}

// checkMove ensures that a given move operation is valid
func (w *Walker) checkMove(lhs common.HIRExpr, rhsDt types.DataType, lhsPos, rhsPos *util.TextPosition) bool {
	if rt, ok := isReference(lhs.Type()); ok {
		if !rt.Owned {
			util.ThrowError(
				"Unable to apply move operation to an unowned reference",
				"Memory",
				lhsPos,
			)

			return false
		}

		if rt.Constant {
			util.ThrowError(
				"Unable to apply move operation to an immutable reference",
				"Mutability",
				lhsPos,
			)

			return false
		}

		if lhs.Category() == common.RValue || lhs.Constant() {
			util.ThrowError(
				"Unable to apply move operation to an immutable value",
				"Mutability",
				lhsPos,
			)

			return false
		}

		if types.CoerceTo(rhsDt, lhs.Type()) {
			return true
		} else {
			ThrowCoercionError(rhsDt, lhs.Type(), rhsPos)
			return false
		}
	}

	util.ThrowError(
		"The left-hand operand of the move operator must be reference",
		"Type",
		lhsPos,
	)

	return false
}

// checkPass ensures that a given value can be passed as the given parameter
func (w *Walker) checkPass(param *types.FuncParam, val common.HIRExpr, valpos *util.TextPosition) bool {
	if rt, ok := isReference(val.Type()); ok {
		if prt, ok := isReference(param.Type); ok {
			if !types.CoerceTo(rt.ElemType, prt.ElemType) || rt.Constant != prt.Constant {
				ThrowCoercionError(rt, prt, valpos)
				return false
			}

			if val.Category() == common.LValue && (prt.Local && !rt.Local || prt.Owned && !rt.Owned) {
				util.ThrowError(
					fmt.Sprintf("Unable to pass a `%s` as a `%s`", rt.Repr(), prt.Repr()),
					"Memory",
					valpos,
				)
				return false
			} else if val.Category() == common.RValue && (prt.Local != rt.Local || prt.Owned != rt.Owned) {
				util.ThrowError(
					fmt.Sprintf("Unable to pass a `%s` as a `%s`", rt.Repr(), prt.Repr()),
					"Memory",
					valpos,
				)
				return false
			}

			return true
		}
	}

	if types.CoerceTo(val.Type(), param.Type) {
		return true
	}

	ThrowCoercionError(val.Type(), param.Type, valpos)
	return false
}

// getContainedReferences recursively unpacks a data type to check for reference
// types and returns a slice of all the reference it finds
func getContainedReferences(dt types.DataType) []*types.ReferenceType {
	return nil
}

// isReference checks if a data type is a reference type and returns the
// reference type it finds.
func isReference(dt types.DataType) (*types.ReferenceType, bool) {
	switch v := dt.(type) {
	case *types.ReferenceType:
		return v, true
	case *types.OpenType:
		if len(v.TypeState) == 1 {
			if rt, ok := v.TypeState[0].(*types.ReferenceType); ok {
				return rt, true
			}
		}
	}

	return nil, false
}
