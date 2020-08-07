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

// walkTypeLabel converts a type label (`type` used as type label) to a DataType
func (w *Walker) walkTypeLabel(branch *syntax.ASTBranch) (types.DataType, bool) {
	return w.walkTypeBranch(branch.BranchAt(0), false)
}

// walkTypeBranch walks any branch that exists at the top level of a `type`
// branch (not just those used in the context of type labels -- supporting type
// parameters) and converts it to a data type
func (w *Walker) walkTypeBranch(branch *syntax.ASTBranch, allowHigherKindedTypes bool) (types.DataType, bool) {
	switch branch.Name {
	case "value_type":
	case "named_type":
	case "ref_type":
		rt := &types.ReferenceType{}

		for _, item := range branch.Content {
			switch v := item.(type) {
			case *syntax.ASTBranch:
				if dt, ok := w.walkTypeBranch(v, false); ok {
					rt.ElemType = dt
				} else {
					return nil, false
				}
			case *syntax.ASTLeaf:
				if v.Kind == syntax.OWN {
					rt.Owned = true
				} else if v.Kind == syntax.CONST {
					rt.Constant = true
				}
			}
		}

		return rt, true
	}

	return nil, false
}
