package analysis

import (
	"errors"
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/types"
)

// NOTES:
// - Should passing an own& to a function as an own& require the original
// own& to be nullable?
// - Allow for a `notnull` statement to override the compiler's nullability
// without an implicit assertion/check (eg. no `if` required)?
// - Allow non-nullable reference to become nullable if they are RValues?
// - Maybe try to figure out a more intuitive system for doing all of this...
//   - Embed category in the reference type so that it can properly coerce
//   based on it (maybe add some additional categories as necessary...)

// nothingType is a constant representing the `nothing` type (ie. void)
var nothingType types.DataType = types.PrimitiveType(types.PrimNothing)

// applyAssign calculates any type changes across the assignment operator NOTE:
// this function makes the shared/view reference nullable unless the source
// reference is constant (stored by a constant symbol).  This behavior should be
// overridden as necessary using the walker's nullability management toolkit.
func applyAssign(expr common.HIRExpr, lhsConst bool) types.DataType {
	if rt, ok := expr.Type().(*types.ReferenceType); ok {
		if rt.Owned {
			if expr.Category() == common.LValue {
				return &types.ReferenceType{
					Owned:    false,
					Constant: rt.Constant,
					Nullable: !lhsConst || rt.Nullable,
					ElemType: rt.ElemType,
				}
			}
		}
	}

	return expr.Type()
}

// checkMove checks if the move operator is valid between two values. It returns
// an error indicating why the operation is invalid if it is determined to be.
// It also returns an integer indicating where the problem is: 0 -- lhs, 1 --
// rhs 2 -- both/operation (-1 is returned if no problem exists).
func checkMove(lhs, rhs common.HIRExpr) (error, int) {
	// TODO: revise move operator to check for open types (on lhs and rhs)
	// There are some corner cases where the move operator can deduce types.
	// What to do if both sides are open?  What judgement should be applied?

	if lrt, ok := lhs.Type().(*types.ReferenceType); ok {
		if rrt, ok := rhs.Type().(*types.ReferenceType); ok && lrt.OwnershipBlindEquals(rrt) {
			if !lrt.Owned {
				return errors.New("Unable to move a unowned reference"), 0
			}

			if rrt.Owned && rhs.Category() == common.LValue {
				return errors.New("Unable to duplicate ownership"), 1
			} else if !rrt.Owned {
				return errors.New("Unable to move to an unowned reference"), 1
			}

			return nil, -1
		}

		return fmt.Errorf("Unable to coerce `%s` to `%s`", lrt.Repr(), rhs.Type().Repr()), 2
	}

	return errors.New("Unable to apply move operator to a non-reference"), 0
}

// checkPass checks if a given value is valid in `in` position (passed in) and
// returns a descriptive error if not.  It also takes a the expected argument type.
func checkPass(expr common.HIRExpr, argType types.DataType) error {
	// TODO: check params recursively?
	if prt, ok := expr.Type().(*types.ReferenceType); ok {
		if art, ok := expr.Type().(*types.ReferenceType); ok && prt.OwnershipBlindEquals(art) {
			if art.Owned && !prt.Owned {
				return errors.New("Unable to pass an unowned reference as an owned argument")
			} else if expr.Category() == common.RValue && !art.Owned {
				return errors.New("Passed R-Value reference requires an owner")
			}

			// all other forms are fine
			return nil
		}
	}

	if types.CoerceTo(expr.Type(), argType) {
		return nil
	}

	return fmt.Errorf("Unable to coerce `%s` to `%s`", expr.Type().Repr(), argType.Repr())
}
