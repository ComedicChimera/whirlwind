package analysis

import (
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/types"
)

// walkTypeExt walks a `type_ext` node and returns the type it denotes
func (w *Walker) walkTypeExt(branch *syntax.ASTBranch) (types.DataType, bool) {
	return w.walkTypeLabel(branch.BranchAt(1))
}

// walkTypeList walks a `type_list` node
func (w *Walker) walkTypeList(branch *syntax.ASTBranch) ([]types.DataType, bool) {
	typeList := make([]types.DataType, branch.Len())

	for i, item := range branch.Content {
		if i%2 == 0 {
			if dt, ok := w.walkTypeLabel(item.(*syntax.ASTBranch)); ok {
				typeList[i/2] = dt
			} else {
				return nil, false
			}
		}
	}

	return typeList, true
}

// walkTypeLabel converts a type label to a DataType
func (w *Walker) walkTypeLabel(branch *syntax.ASTBranch) (types.DataType, bool) {
	return nil, false
}
