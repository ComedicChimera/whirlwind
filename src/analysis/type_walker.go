package analysis

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
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
		// TODO: free types/resolving types

		var genericTypeSpec []types.DataType
		var pnames []PositionedName

		for i := branch.Len() - 1; i >= 0; i-- {
			switch v := branch.Content[i].(type) {
			case *syntax.ASTLeaf:
				if v.Kind == syntax.IDENTIFIER {
					// I love having to type `PositionedName` TWO TIMES just to prepend...
					pnames = append([]PositionedName{
						PositionedName{Name: v.Value, Pos: v.Position()},
					}, pnames...)
				}
			case *syntax.ASTBranch:
				// v.Name always == "type_list"
				if typeList, ok := w.walkTypeList(v); ok {
					genericTypeSpec = typeList
				} else {
					return nil, false
				}
			}
		}

		var dsym *common.Symbol
		if len(pnames) == 1 {
			pname := pnames[0]
			dsym = w.Lookup(pname.Name)

			if dsym == nil {
				ThrowUndefinedError(dsym.Name, pname.Pos)
				return nil, false
			}
		} else {
			sym, err := w.GetSymbolFromPackage(pnames)

			if err != nil {
				util.LogMod.LogError(err)

				return nil, false
			}

			dsym = sym
		}

		if dsym.DefKind != common.SKindTypeDef {
			ThrowSymbolUsageError(dsym.Name, "type definition", pnames[len(pnames)-1].Pos)
			return nil, false
		}

		if len(genericTypeSpec) > 0 {
			if gt, ok := dsym.Type.(*types.GenericType); ok {
				if generate, ok := gt.CreateGenerate(genericTypeSpec); ok {
					return generate, true
				}

				util.ThrowError(
					fmt.Sprintf("Invalid type parameters for the generic type `%s`", gt.Repr()),
					"Type",
					branch.Last().Position(),
				)
			}

			util.ThrowError(
				"Type parameters can only be passed to a generic type",
				"Type",
				branch.Last().Position(),
			)
		} else {
			return dsym.Type, true
		}
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
				switch v.Kind {
				case syntax.OWN:
					rt.Owned = true
				case syntax.CONST:
					rt.Constant = true
				case syntax.NULLTEST:
					rt.Nullable = true
				}
			}
		}

		return rt, true
	}

	return nil, false
}
