package resolve

import "whirlwind/common"

// The Resolution Algorithm
// ------------------------
// 1. Go through every definition in the resolution unit and extract every type
// or interface definition in those packages and determine if they depend on any
// unknown symbols.  If they do, add them to the resolution queue and table; if
// they don't automatically define them.  We want to isolate these two kinds of
// definitions since at the top level, they are the only definitions that can
// cause other definitions to resolve (eg. a function definition can never be
// referenced directly as a type which at the top level is the only kind of
// definition interdependency).  These definitions are known as "determinate
// definitions".
// 2.

// Resolver is main construct responsible for symbol resolution and high-level
// package assembly (it implements the resolution algorithm described above). It
// operates on entire resolution units as opposed to individual packages.
type Resolver struct {
	// assemblers stores a list of all the package assemblers for this
	// resolution unit
	assembers map[uint]*PAssembler

	// depGraph is the dependency graph constructed by the compiler
	depGraph map[uint]*common.WhirlPackage
}

// NewResolver creates a new resolver for the given set of packages
func NewResolver(pkgs []*common.WhirlPackage, depg map[uint]*common.WhirlPackage) *Resolver {
	r := &Resolver{
		assembers: make(map[uint]*PAssembler),
		depGraph:  depg,
	}

	for _, pkg := range pkgs {
		r.assembers[pkg.PackageID] = &PAssembler{
			SrcPackage: pkg,
		}
	}

	return r
}

// Resolve runs the main resolution algorithm on all the packages in resolution
func (r *Resolver) Resolve() bool {
	allResolved := true
	for _, pa := range r.assembers {
		allResolved = allResolved && pa.initialPass()
	}

	if allResolved {
		return true
	}

	return false
}
