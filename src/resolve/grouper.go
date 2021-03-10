package resolve

import (
	"whirlwind/common"
)

// Grouping Algorithm
// ------------------
// Definitions/Rules:
// - A `cycle root` is a package that is awaiting resolution and encountered
//   as a dependency of another package during grouping.
// - A group/unit that is considered a `resolution unit` is immediately resolved
//   and the unit is to be cleared.
// - Any group that fails to resolve is grounds for immediate failure of grouping.
// Algorithm:
// 1. Start at the root package.
// 2. From the current package, evaluate all of its connections.
// 3. If all of its connections resolve, consider the current package
//    its own discrete group.
// 4. If one or more of its connections don't fully resolve, then:
//    i. If one cycle root is determined, then
//       a. If the current package is the cycle root, consider all
//          packages in the current unit + the current package a
//          resolution group.
//       b. If the current package is not the cycle root, add it to
//          the the current unit and return.
//    ii. If there is more than one cycle root determined, then
//       a. If all of the cycle roots are the in the current unit,
//          consider the current unit a resolution unit.
//       b. Otherwise, add the current package to the current unit
//          and return.
//

// Grouper is responsible for analyzing the package graph and determining the
// resolution units to be passed to the resolver.  The grouper also passes the
// groups it creates to the resolver as it creates them.
type Grouper struct {
	// RootPackage is the package from which compilation began (for applications
	// this the main package -- the package to be built into an executable).
	RootPackage *common.WhirlPackage

	// DepGraph is the dependency graph constructed by the compiler to be analyzed
	DepGraph map[uint]*common.WhirlPackage

	// ResolutionStatuses stores the IDs of the packages that have a known
	// resolution status.  A false value indicates that a package has yet to be
	// resolved a true value indicates that it has already been resolved.
	ResolutionStatuses map[uint]bool

	// CurrentUnit is the resolution unit being constructed
	CurrentUnit map[uint]*common.WhirlPackage
}

// NewGrouper creates a new grouper for the given dep-g and root package
func NewGrouper(rootpkg *common.WhirlPackage, depg map[uint]*common.WhirlPackage) *Grouper {
	return &Grouper{
		RootPackage:        rootpkg,
		DepGraph:           depg,
		ResolutionStatuses: make(map[uint]bool),
		CurrentUnit:        make(map[uint]*common.WhirlPackage),
	}
}

// ResolveAll runs the grouping algorithm starting from the root package and
// resolves all the groups as it arranges them.
func (g *Grouper) ResolveAll() bool {
	_, ok := g.groupFrom(g.RootPackage)
	return ok
}

// groupFrom contains the main grouping and resolution algorithm (as described
// above). The return values from this function are: the cycle roots -- this
// will be `nil` if there is no cycle root (we are resolving linearly) and a
// boolean indicating if the resolution of the previous groups was successful.
// Since this function works recursively, any failure in resolution will more
// often than not cause issues with every other package above it that depends on
// it.  Therefore, in order to prevent a cascade of meaningless failures, we
// consider resolution failure a base case for recursion -- prompting an
// immediate unwind without any further grouping or analysis.
func (g *Grouper) groupFrom(pkg *common.WhirlPackage) (map[uint]struct{}, bool) {
	// mark the current package as awaiting resolution
	g.ResolutionStatuses[pkg.PackageID] = false

	// store the accumulated cycle roots
	roots := make(map[uint]struct{})
	for id := range pkg.ImportTable {
		// if the package has a known status and has not already been resolved,
		// then we consider that package to be a cycle root.
		if status, ok := g.ResolutionStatuses[id]; ok {
			if !status {
				roots[id] = struct{}{}
			}
		} else if nroots, ok := g.groupFrom(g.DepGraph[id]); ok {
			// if the package is unknown (ungrouped), then we explore its path
			// and if it is groupable, we add its new roots to our current cycle
			// roots
			for nroot := range nroots {
				roots[nroot] = struct{}{}
			}
		} else {
			// if the package has no status and fails to group, then is has
			// failed resolution and we need to fail immediately
			return nil, false
		}
	}

	switch len(roots) {
	case 0:
		// if we have no cycle roots, then we can consider the current single package
		// its own distinct resolution unit and resolve it accordingly
		rok := g.resolveSingle(pkg)
		return nil, rok
	case 1:
		// if there is only one cycle root, we add the current package to the
		// current unit (happens in either case) and then we check if the
		// current package is the cycle root and if it is, we resolve the whole
		// unit and clear it.  Otherwise, we return.
		g.CurrentUnit[pkg.PackageID] = pkg

		if _, ok := roots[pkg.PackageID]; ok {
			rok := g.resolveCurrentUnit()
			return nil, rok
		}

		return roots, true
	default:
		// if there are multiple cycle roots, we determine first if all the
		// roots are in the current unit.  If they are not, then we add the
		// current package to the current unit and return.
		for root := range roots {
			if _, ok := g.CurrentUnit[root]; !ok {
				g.CurrentUnit[pkg.PackageID] = pkg
				return roots, true
			}
		}

		// if all the cycle roots are in the current unit, then we resolve and
		// clear the current unit, and then resolve the current package
		// independently.
		rok := g.resolveCurrentUnit()

		if !rok {
			return nil, false
		}

		rok = g.resolveSingle(pkg)
		return nil, rok
	}
}

// resolveSingle resolves a single package as a distinct resolution unit
func (g *Grouper) resolveSingle(pkg *common.WhirlPackage) bool {
	// whether or not resolution succeeds is unimportant.  If it fails, the
	// statuses won't matter anyway
	g.ResolutionStatuses[pkg.PackageID] = true

	r := NewResolver([]*common.WhirlPackage{pkg}, g.DepGraph)
	return r.Resolve()
}

// resolveCurrentUnit resolves the current unit as one resolution unit and
// clears the current unit.
func (g *Grouper) resolveCurrentUnit() bool {
	// whether or not resolution succeeds is unimportant.  If it fails, the
	// statuses won't matter anyway -- mark every package as resolved,
	// create the resolution unit, and clear the current unit in one pass
	runit := make([]*common.WhirlPackage, len(g.CurrentUnit))

	n := 0
	for id, pkg := range g.CurrentUnit {
		g.ResolutionStatuses[id] = true
		runit[n] = pkg
		n++

		delete(g.CurrentUnit, id)
	}

	// resolve the current unit and return the status
	r := NewResolver(runit, g.DepGraph)
	return r.Resolve()
}
