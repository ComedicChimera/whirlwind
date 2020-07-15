package analysis

import (
	"github.com/ComedicChimera/whirlwind/src/semantic"
	"github.com/ComedicChimera/whirlwind/src/semantic/depm"
	"github.com/ComedicChimera/whirlwind/src/types"
)

// Walker is used to walk down a file AST, perform semantic analysis and
// checking, and convert it into a HIR tree.  The Walker first walks through the
// top level of a file and then walks down the predicates.
type Walker struct {
	Builder *PackageBuilder
	File    *depm.WhirlFile

	// Root represents the root of the currently parsed file
	Root *semantic.HIRRoot

	// Scopes is used to represent the local, enclosing scopes of functions and
	// blocks.  It is not preserved b/c the back-end doesn't actually need the
	// local scopes to generate the output code.
	Scopes []*Scope
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

// WalkTop begins walking a file from the top level
func (w *Walker) WalkTop() bool {
	return true
}

// WalkPredicates walks all of the unevaluated predicates
func (w *Walker) WalkPredicates() bool {
	return true
}
