package analysis

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// ThrowMultiDefError will log an error indicating that a symbol of a given name
// is declared multiple times in the current scope.
func ThrowMultiDefError(name string, pos *util.TextPosition) {
	util.ThrowError(
		fmt.Sprintf("Symbol `%s` declared multiple times", name),
		"Name",
		pos,
	)
}

// ThrowUndefinedError will log a symbol undefined error
func ThrowUndefinedError(name string, pos *util.TextPosition) {
	util.ThrowError(
		fmt.Sprintf("Symbol `%s` undefined", name),
		"Name",
		pos,
	)
}

// ThrowSymbolUsageError will log a misuse of a symbol (eg. something is used in
// type label that is not a type def)
func ThrowSymbolUsageError(name, expectedUsage string, pos *util.TextPosition) {
	util.ThrowError(
		fmt.Sprintf("Symbol `%s` is not a %s", name, expectedUsage),
		"Usage",
		pos,
	)
}

// ThrowCoercionError throws an error indicating that a type was not coercible
// to another type
func ThrowCoercionError(src, dest types.DataType, pos *util.TextPosition) {
	util.ThrowError(
		fmt.Sprintf("Unable to coerce `%s` to `%s`", src.Repr(), dest.Repr()),
		"Type",
		pos,
	)
}
