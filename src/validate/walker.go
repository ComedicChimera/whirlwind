package validate

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/logging"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/typing"
)

// Walker is used to walk the ASTs of files, validate them, and translate them
// into HIR.  It also handles symbol lookups and definitions within files.
type Walker struct {
	SrcPackage *common.WhirlPackage
	SrcFile    *common.WhirlFile
	Context    *logging.LogContext

	// DeclStatus is a field set by the resolver to indicate the decl status of
	// all newly defined symbols.  It is set to `DSInternal` by default.
	DeclStatus int

	// unknowns is a map of all of the unknown symbols for a given definition.
	// This is used by the PAssembler and Resolver during symbol resolution.
	// If this field is set to `nil`, then resolution is finished and this field
	// can be ignored.
	unknowns map[string]*UnknownSymbol

	// Solver stores the type solver that is used for inference and type deduction
	solver *typing.Solver

	// FatalDefError is a flag that is used to mark when an error that occurred in
	// a definition is fatal (ie. not related to an unknown)
	fatalDefError bool

	// GenericCtx stores a list of the generic wildcard types in use during
	// declaration so a generic can be formed after.  This field is also used as
	// a flag to indicate whether or not a generic is use (if it is not nil,
	// there is a generic)
	genericCtx []*typing.WildcardType

	// annotations stores the active annotations on any definition
	annotations map[string]string

	// selfType stores a reference to the type currently being defined for self referencing
	selfType typing.DataType

	// selfTypeUsed indicates whether or not the self type reference was used
	selfTypeUsed bool
}

// UnknownSymbol is a symbol awaiting resolution
type UnknownSymbol struct {
	Name     string
	Position *logging.TextPosition

	// ForeignPackage is the location where this symbol is expected to be found
	// (nil if it belongs to the current package or an unknown package --
	// namespace import).
	ForeignPackage *common.WhirlPackage

	// ImplicitImport is used to indicate whether or not a symbol is implicitly
	// imported. This field is meaningless if the SrcPackage field is nil.
	ImplicitImport bool
}

// NewWalker creates a new walker for the given package and file
func NewWalker(pkg *common.WhirlPackage, file *common.WhirlFile, fpath string) *Walker {
	// initialize the files local binding registry (may decide to remove this as
	// a file field if it is not helpful/necessary and instead embed as a walker
	// field)
	file.LocalBindings = &typing.BindingRegistry{}

	return &Walker{
		SrcPackage: pkg,
		SrcFile:    file,
		Context: &logging.LogContext{
			PackageID: pkg.PackageID,
			FilePath:  fpath,
		},
		DeclStatus: common.DSInternal,
		unknowns:   make(map[string]*UnknownSymbol),
		solver: &typing.Solver{
			GlobalBindings: pkg.GlobalBindings,
			LocalBindings:  file.LocalBindings,
		},
	}
}

// resolutionDone indicates to the walker that resolution has finished.
func (w *Walker) resolutionDone() {
	w.unknowns = nil
}

// hasFlag checks if the given annotation is active (as a flag; eg. `#packed`)
func (w *Walker) hasFlag(flag string) bool {
	_, ok := w.annotations[flag]
	return ok
}

// walkIdList walks a list of identifiers and returns a map of names and
// positions (for error handling)
func (w *Walker) walkIdList(idList *syntax.ASTBranch) map[string]*logging.TextPosition {
	names := make(map[string]*logging.TextPosition)

	for i, item := range idList.Content {
		if i%2 == 0 {
			names[item.(*syntax.ASTLeaf).Value] = item.Position()
		}
	}

	return names
}
