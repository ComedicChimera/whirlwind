package analysis

import (
	"github.com/ComedicChimera/whirlwind/src/semantic/depm"
	"github.com/ComedicChimera/whirlwind/src/types"
)

// PackageBuilder is a construct used to build an initialized package into a
// fully analyzed and converted HIR package.  Its output is ready for processing
// by the back-end.
type PackageBuilder struct {
	Pkg *depm.WhirlPackage

	// Walkers stores a list of all of the file specific Walkers
	Walkers []*Walker

	// ResolvingSymbols contains a list of symbols that are currently undefined
	// at the top level of packages.  These symbols must be resolved before the
	// builder can make the Walkers proceed to next phase of compilation.
	// Unresolved symbols will have a `nil` DataType reference (the DataTypes
	// correspond to references inside TypePlaceholders)
	ResolvingSymbols map[string]*types.DataType
}

// BuildPackage fully builds a package.
func (pb *PackageBuilder) BuildPackage() bool {
	for _, wf := range pb.Pkg.Files {
		walker := NewWalker(pb, wf)
		pb.Walkers = append(pb.Walkers, walker)

		if !walker.WalkTop() {
			return false
		}

		wf.Root = walker.Root
	}

	// reference to root is shared so this isn't a problem
	for _, walker := range pb.Walkers {
		if !walker.WalkPredicates() {
			return false
		}
	}

	return true
}
