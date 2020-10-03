package assemble

import (
	"github.com/ComedicChimera/whirlwind/src/common"
)

// The Package Assembly Algorithm
// ------------------------------
// 1. Create resolvers for all of the packages in the current assembly group.
// 2  Resolve all local symbols using the single-package symbol resolution
//    algorithm described in `resolver.go`.
// 3. Consider all of the external, unresolved symbols as part of one definition
//    queue and attempt to resolve them with cross resolution.  This algorithm
//    works much the same as the single-package resolution algorithm just using
//    multiple definition queues.

// PAssembler is the construct responsible for managing package assembly --
// construction of top-level of a package or packages.  It is the implementation
// of compilation Stage 2.
type PAssembler struct {
	// Resolvers is the map of resolvers for all the packages in the given assembly
	// group.  They are organized by package ID.  This is also our only reference
	// to the packages being resolved -- we don't need to store it multiple times.
	Resolvers map[uint]*Resolver
}

// NewPackageAssembler creates a new assembler for the given set of packages
func NewPackageAssembler(pkgs ...*common.WhirlPackage) *PAssembler {
	pa := &PAssembler{Resolvers: make(map[uint]*Resolver)}

	for _, pkg := range pkgs {
		pa.Resolvers[pkg.PackageID] = NewResolverForPackage(pkg)
	}

	return pa
}

// Assemble runs the main cross-resolution and assembly algorithm
func (pa *PAssembler) Assemble() bool {
	for _, r := range pa.Resolvers {
		if !r.ResolveLocals() {
			return false
		}
	}

	return true
}
