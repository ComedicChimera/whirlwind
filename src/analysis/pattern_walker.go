package analysis

import (
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// UnpackedVar is a simple struct for storing the data extracted from an
// `unpack_var` node efficiently.
type UnpackedVar struct {
	Type types.DataType
	Pos  *util.TextPosition
}

// walkTupleUnpackVar walks an `unpack_var` node in reference to a provided tuple
// type and extracts all of the types, values, and positions contained.
func (w *Walker) walkTupleUnpackVar(b *syntax.ASTBranch, tt *types.TupleType) (map[string]*UnpackedVar, bool) {
	return nil, false
}
