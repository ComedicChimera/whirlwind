package analysis

import (
	"github.com/ComedicChimera/whirlwind/src/semantic"
	"github.com/ComedicChimera/whirlwind/src/semantic/depm"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/types"
)

// Walker is used to walk down a file AST, perform semantic analysis and
// checking, and convert it into a HIR tree.  The Walker first walks through the
// top level of a file and then walks down the predicates.  NOTE: all walk
// functions return true if they FAIL (ie. there is an error).
type Walker struct {
	Builder *PackageBuilder
	File    *depm.WhirlFile

	// Root represents the root of the currently parsed file
	Root *semantic.HIRRoot

	// Scopes is used to represent the local, enclosing scopes of functions and
	// blocks.  It is not preserved b/c the back-end doesn't actually need the
	// local scopes to generate the output code.
	Scopes []*Scope

	// ExportSymbols is used to denote that the Walker is currently within an
	// export block and should export any symbols it adds to the global table
	ExportSymbols bool
}

// Scope represents an enclosing local scope
type Scope struct {
	Symbols map[string]*semantic.Symbol

	// Kind is represents what type of scope this is (const, mutable, or unknown)
	Kind int

	// NonNullRefs stores all of the references who due to some enclosing operation
	// have been marked as non-null in contrast to their data type (by name)
	NonNullRefs map[string]struct{}

	// CtxFunc is the enclosing function of a scope.  May be `nil` under certain
	// circumstances (eg. inside an interface definition but outside of a method)
	CtxFunc *types.FuncType
}

// Enumeration of the different scope kinds
const (
	SKConst = iota
	SKMutable
	SKUnknown // default scope kind
)

// NewWalker creates a new walker for a file in a given package
func NewWalker(pb *PackageBuilder, file *depm.WhirlFile) *Walker {
	return &Walker{Builder: pb, File: file, Root: &semantic.HIRRoot{}}
}

// WalkFile begins walking a file from the top level
func (w *Walker) WalkFile() bool {
	// we don't do any error-suppressing in this function as if a definition
	// doesn't pass, it cause a slough of errors in other definitions that
	// aren't actually useful to the end user and just serve to clutter up
	// compiler output.
	for _, item := range w.File.AST.Content {
		// all top-level items are ASTBranches
		branch := item.(*syntax.ASTBranch)

		// we should already have collected imports at this point so we don't
		// need to check for them here.
		switch branch.Name {
		case "export_block":
			w.ExportSymbols = true

			// the `top_level` node is always the third node in `export_block`
			if w.walkDefinitions(branch.Content[2].(*syntax.ASTBranch)) {
				return true
			}

			w.ExportSymbols = false
		case "top_level":
			if w.walkDefinitions(branch) {
				return true
			}
		}
	}

	return false
}

// WalkPredicates walks all of the unevaluated predicates
func (w *Walker) WalkPredicates() bool {
	return true
}
