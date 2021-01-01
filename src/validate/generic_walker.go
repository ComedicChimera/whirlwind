package validate

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/typing"
)

// primeGenericContext should be called whenever a `generic_tag` node is
// encountered in a definition.  It parses the tag and appropriately populates
// the `TypeParams` slice with all of the encounter type parameters.  It returns
// `true` if the parsing was successful.  This function does populate the
// `Unknowns` field and sets `FatalDefError` appropriately.
func (w *Walker) primeGenericContext(genericTag *syntax.ASTBranch) bool {
	return false
}

// applyGenericContext should be called at the end of every definition.  This
// function checks if a generic context exists (in `TypeParams`).  If so,
// returns the appropriately constructed `HIRGeneric` and clears the generic
// context for the next definition.  If there is no context, it simply returns
// the definition passed in.
func (w *Walker) applyGenericContext(node common.HIRNode, gdt typing.DataType) common.HIRNode {
	if w.GenericCtx == nil {
		return node
	}

	gen := &common.HIRGeneric{
		Generic: &typing.GenericType{
			TypeParams: w.GenericCtx,
			Template:   gdt,
		},
		GenericNode: node,
	}

	w.GenericCtx = nil
	return gen
}
