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
	unknowns map[string]*common.UnknownSymbol

	// Solver stores the type solver that is used for inference and type deduction
	solver *typing.Solver

	// resolving indicates whether or not the package that contains the file
	// the walker is analyzing has been fully resolved
	resolving bool

	// fatalDefError is a flag that is used to mark when an error that occurred in
	// a definition is fatal (ie. not related to an unknown)
	fatalDefError bool

	// GenericCtx stores a list of the generic wildcard types in use during
	// declaration so a generic can be formed after.  This field is also used as
	// a flag to indicate whether or not a generic is use (if it is not nil,
	// there is a generic)
	genericCtx []*typing.WildcardType

	// annotations stores the active annotations on any definition
	annotations map[string]string

	// selfType stores a reference to the type currently being defined for self
	// referencing
	selfType typing.DataType

	// selfTypeUsed indicates whether or not the self type reference was used
	selfTypeUsed bool

	// selfTypeRequiresRef stores a flag indicating whether or not the self type
	// must be accessed via a reference (eg. for structs)
	selfTypeRequiresRef bool

	// sharedOpaqueSymbol stores a common opaque symbol reference to be given to
	// all package assemblers and shared with all walkers.  It used during
	// cyclic dependency resolution.
	sharedOpaqueSymbol *common.OpaqueSymbol

	// currentDefName stores the symbol name of the current definition being
	// processed
	currentDefName string
}

// NewWalker creates a new walker for the given package and file
func NewWalker(pkg *common.WhirlPackage, file *common.WhirlFile, fpath string, sos *common.OpaqueSymbol) *Walker {
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
		unknowns:   make(map[string]*common.UnknownSymbol),
		solver: &typing.Solver{
			GlobalBindings: pkg.GlobalBindings,
			LocalBindings:  file.LocalBindings,
		},
		resolving:          true, // start in resolution by default
		sharedOpaqueSymbol: sos,
	}
}

// resolutionDone indicates to the walker that resolution has finished.
func (w *Walker) resolutionDone() {
	w.unknowns = nil
	w.resolving = false
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
