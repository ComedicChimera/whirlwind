package assemble

import (
	"github.com/ComedicChimera/whirlwind/src/common"
)

// PAssembler is the construct responsible for managing package assembly --
// construction of top-level of a package or packages.  It is the implementation
// of compilation Stage 2.
type PAssembler struct {
	// Packages is the map of packages that are being assembled (by ID) along
	// with their associated resolution table.  The package ref is embedded in
	// the resolution table so we don't need to store it two times.
	Packages map[uint]*ResolutionTable

	// DefQueue is the queue of definitions being resolved in all packages
	DefQueue *DefinitionQueue
}

// NewPackageAssembler creates a new assembler for the given set of packages
func NewPackageAssembler(pkgs ...*common.WhirlPackage) *PAssembler {
	pa := &PAssembler{Packages: make(map[uint]*ResolutionTable), DefQueue: &DefinitionQueue{}}

	for _, pkg := range pkgs {
		pa.Packages[pkg.PackageID] = &ResolutionTable{
			CurrPkg:  pkg,
			Unknowns: make(map[string]struct{}),
		}
	}

	return pa
}

// Assemble runs the main cross-resolution and assembly algorithm
func (pa *PAssembler) Assemble() bool {
	return false
}
