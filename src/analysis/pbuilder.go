package analysis

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// PackageBuilder is a construct used to build an initialized package into a
// fully analyzed and converted HIR package.  Its output is ready for processing
// by the back-end.
type PackageBuilder struct {
	Pkg *common.WhirlPackage

	// Walkers stores a list of all of the file specific Walkers
	Walkers []*Walker

	// ResolvingSymbols contains a list of symbols that are currently undefined
	// at the top level of packages.  These symbols must be resolved before the
	// builder can make the Walkers proceed to next phase of compilation.
	// Unresolved symbols will have a `nil` Symbol reference
	ResolvingSymbols map[string]struct {
		SymRef *common.Symbol
		SymPos *util.TextPosition
	}
}

// BuildPackage fully builds a package.  Returns true if package construction
// suceeds (and false if it fails, opposite of walker)
func (pb *PackageBuilder) BuildPackage() bool {
	for _, wf := range pb.Pkg.Files {
		walker := NewWalker(pb, wf)
		pb.Walkers = append(pb.Walkers, walker)

		if walker.WalkFile() {
			return false
		}

		wf.Root = walker.Root
	}

	// check for unresolved symbols
	allSymbolsResolved := true
	for name, rs := range pb.ResolvingSymbols {
		if rs.SymRef == nil {
			allSymbolsResolved = false
			util.LogMod.LogError(
				util.NewWhirlError(
					fmt.Sprintf("Undefined Symbol: `%s`", name),
					"Name",
					rs.SymPos,
				),
			)
		}
	}

	if !allSymbolsResolved {
		return false
	}

	// reference to root is shared so this isn't a problem
	for _, walker := range pb.Walkers {
		if walker.WalkPredicates() {
			return false
		}
	}

	return true
}
