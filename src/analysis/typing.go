package analysis

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/types"
)

// nothingType is a constant representing the `nothing` type (ie. void)
var nothingType types.DataType = types.PrimitiveType(types.PrimNothing)

// applyAssign calculates any type changes across the assignment operator
func applyAssign(expr common.HIRExpr) types.DataType {
	return nil
}

// checkReturn checks if a given value is valid in `out` position (returned) and
// returns a descriptive error if not
func checkReturn(expr common.HIRExpr) error {
	return nil
}

// checkPass checks if a given value is valid in `in` position (passed in) and
// returns a descriptive error if not
func checkPass(expr common.HIRExpr) error {
	return nil
}
