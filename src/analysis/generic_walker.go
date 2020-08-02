package analysis

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// setupGenericContext walks a `generic_tag`, extracts and stores it type
// parameters and primes the walker to create a generic
func (w *Walker) setupGenericContext(branch *syntax.ASTBranch) bool {
	for _, item := range branch.Content {
		// only branch kind is `generic_param`
		if sb, ok := item.(*syntax.ASTBranch); ok {
			tp := &types.TypeParam{}

			for _, elem := range sb.Content {
				switch v := elem.(type) {
				case *syntax.ASTBranch:
					if dt, ok := w.walkTypeLabel(v); ok {
						for _, res := range tp.Restrictors {
							if types.CoerceTo(dt, res) || types.CoerceTo(res, dt) {
								util.ThrowError(
									"Restrictor includes redundant type members",
									"Type",
									v.Position(),
								)

								return false
							}
						}

						tp.Restrictors = append(tp.Restrictors, dt)
					}
				case *syntax.ASTLeaf:
					if v.Kind == syntax.IDENTIFIER {
						if _, exists := w.TypeParams[v.Value]; exists {
							util.ThrowError(
								fmt.Sprintf("Duplicate type parameter name: `%s`", v.Value),
								"Name",
								v.Position(),
							)

							return false
						}

						tp.Name = v.Value
					}
				}
			}

			w.TypeParams[tp.Name] = tp
		}
	}

	return true
}

// makeGeneric creates a new generic from the given node and symbol based on the
// global set of TypeParameters.  Also clears the generic context.  Mutates
// the symbols data type.  Does not perform any actual declaration.
func (w *Walker) makeGeneric(sym *common.Symbol, node common.HIRNode) common.HIRNode {
	gtps := make([]*types.TypeParam, len(w.TypeParams))
	n := 0
	for _, tp := range w.TypeParams {
		gtps[n] = tp
		n++
	}

	w.TypeParams = nil

	gen := types.NewGenericType(sym.Type, gtps)

	return &common.HIRGeneric{
		Generic:     gen,
		GenericNode: node,
	}
}
