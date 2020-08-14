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
	newCtx := make(map[string]*types.TypeParam)

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
						if _, exists := newCtx[tp.Name]; exists {
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

			newCtx[tp.Name] = tp
		}
	}

	w.GenericContextStack = append(w.GenericContextStack, newCtx)
	return true
}

// makeGeneric creates a new generic from the given node and symbol based on the
// global set of TypeParameters.  Also clears the generic context.  Mutates
// the symbols data type.  Does not perform any actual declaration.
func (w *Walker) makeGeneric(sym *common.Symbol, node common.HIRNode) common.HIRNode {
	gctx := w.popGenericContext()

	gtps := make([]*types.TypeParam, len(gctx))
	n := 0
	for _, tp := range gctx {
		gtps[n] = tp
		n++
	}

	gen := types.NewGenericType(sym.Type, gtps)

	return &common.HIRGeneric{
		Generic:     gen,
		GenericNode: node,
	}
}

// shouldCreateGeneric checks the current context to see if a generic needs to
// be created (intended primarily for use in the top-level stage of walking)
func (w *Walker) shouldCreateGeneric() bool {
	return len(w.GenericContextStack) > 0
}

// popGenericContext gets the current generic context if one exists and pops it
// off the generic context stack
func (w *Walker) popGenericContext() map[string]*types.TypeParam {
	currCtx := w.GenericContextStack[len(w.GenericContextStack)-1]
	w.GenericContextStack = w.GenericContextStack[:len(w.GenericContextStack)-1]
	return currCtx
}
