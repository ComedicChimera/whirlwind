package validate

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/logging"
)

// Walker is used to walk the ASTs of files, validate them, and translate them
// into HIR.  It also handles symbol lookups and definitions within files.
type Walker struct {
	SrcPackage *common.WhirlPackage
	SrcFile    *common.WhirlFile
	Context    *logging.LogContext

	// Unknowns is a map of all of the unknown symbols for a given definition.
	// This is used by the PAssembler and Resolver during symbol resolution.
	// If this field is set to `nil`, then resolution is finished and this field
	// can be ignored.
	Unknowns map[string]*UnknownSymbol
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
	return &Walker{
		SrcPackage: pkg,
		SrcFile:    file,
		Context: &logging.LogContext{
			PackageID: pkg.PackageID,
			FilePath:  fpath,
		},
		Unknowns: make(map[string]*UnknownSymbol),
	}
}

// resolutionDone indicates to the walker that resolution has finished.
func (w *Walker) resolutionDone() {
	w.Unknowns = nil
}
