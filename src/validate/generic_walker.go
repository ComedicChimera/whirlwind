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
	// TODO: handle generic self-references NOTE: it could simply be a
	// combination of the existing self-reference model along with a genericCtx
	// test (for `walkTypeLabel` to tell if the generic initializer is valid)

	return false
}

// applyGenericContext should be called at the end of every definition.  This
// function checks if a generic context exists (in `TypeParams`).  If so,
// returns the appropriately constructed `HIRGeneric` and clears the generic
// context for the next definition.  If there is no context, it simply returns
// the definition passed in.
func (w *Walker) applyGenericContext(node common.HIRNode, name string) common.HIRNode {
	if w.genericCtx == nil {
		return node
	}

	// find the symbol of the declared data type so it can be updated (should
	// always succeed b/c this is called immediately after a definition)
	symbol, _ := w.Lookup(name)
	gen := &common.HIRGeneric{
		Generic: &typing.GenericType{
			TypeParams: w.genericCtx,
			Template:   symbol.Type,
		},
		GenericNode: node,
	}

	// override the symbol's type
	symbol.Type = gen.Generic

	// TODO: update algebraic instances of open generic algebraic types

	w.genericCtx = nil
	return gen
}

// createGenericInstance creates a new instance of the given generic type
func (w *Walker) createGenericInstance(generic *typing.GenericType, params []typing.DataType) (typing.DataType, bool) {
	return nil, false
}
