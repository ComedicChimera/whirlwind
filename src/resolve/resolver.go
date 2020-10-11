package resolve

import (
	"github.com/ComedicChimera/whirlwind/src/common"
)

// The Resolution Algorithm
// ------------------------
// 1. Import and resolve all known remote symbols that are not apart of the
//    current resolution unit.
// 2. Perform an initial resolution pass on each package in the unit such that
//    all symbols that depend only predefined or known symbols are finalized,
//    declared, and prepared for validation.  All other symbols are then converted
//    into definitions and added to their appropriate definition queues.  In this
//    process all necessary package assemblers and validation walkers are created.
// 3. Select an arbitrary package to be the start of our resolution.  Begin resolution
//    with that specific package's definition queue considering it to be the current
//    queue.  Pass over the current queue as follows:
//    a. If the top definition is resolveable, finalize it and remove it from the queue.
//    b. If the top definition still has unknowns in another package, consider
//    that package's queue to be the current resolution unless the definition at the top
//    of that package's queue has already been considered the top of a current queue and
//    is not currently resolveable in which case, the top definition
// 4. Consider all remaining symbols fatally unresolveable and log appropriate errors.

// Resolver is the main abstraction for package resolution. It is responsible
// for resolving all of the symbols in a single package as well as those
// shared/cross-reference by multiple packages.  The resolver operates in
// resolution units wherein each unit is composed of one or more packages that
// are mutually dependent.  It facilitates the Resolution Algorithm.
type Resolver struct {
	Assemblers map[uint]*PAssembler
}

// NewResolver creates a new resolver for the given group of packages
func NewResolver(pkgs []*common.WhirlPackage) *Resolver {
	r := &Resolver{Assemblers: make(map[uint]*PAssembler)}

	for _, pkg := range pkgs {
		r.Assemblers[pkg.PackageID] = NewPackageAssembler(pkg)
	}

	return r
}

// Resolve runs the resolution algorithm with the current resolver.
func (r *Resolver) Resolve() bool {
	// step 1 -- if this step fails, then so will resolution so we exit here and
	// return a failure status.
	if !r.resolveKnownImports() {
		return false
	}

	// step 2 -- initial passes
	for _, pa := range r.Assemblers {
		pa.initialPass()
	}

	// step 3 -- resolution

	// step 4 -- log appropriate errors and determine exit status
	allresolved := true
	for _, pa := range r.Assemblers {
		if pa.DefQueue.Len() > 0 {
			pa.logUnresolved()
		}
	}

	return allresolved
}

// resolveKnownImports resolves all known imports.  It fails if an explicit import
// on a known/already resolved package is undefined/not visible.
func (r *Resolver) resolveKnownImports() bool {
	// flag used to indicate if all known imports resolved (so we can log all
	// that don't)
	allresolved := true

	for _, pa := range r.Assemblers {
		for pkgid, wimport := range pa.PackageRef.ImportTable {
			// we don't consider an import known if it is in our resolution unit
			if _, ok := r.Assemblers[pkgid]; !ok {
				for name, sref := range wimport.ImportedSymbols {
					if !r.resolveSymImport(pkgid, name, sref) {
						// log errors on all attempts to import this symbol explicitly
						for _, wfile := range pa.PackageRef.Files {
							if wsi, ok := wfile.LocalTable[name]; ok && wsi.SrcPackage.PackageID == pkgid {
								pa.Walkers[wfile].LogNotVisibleInPackage(name, wsi.SrcPackage.Name, wsi.Position)
							}
						}

						allresolved = false
					}
				}
			}
		}
	}

	return allresolved
}

// resolveSymImport attempts to resolve an explicitly-imported or
// namespace-imported symbol from another package with a given symbol reference.
// This function does not log an error if the symbol does not resolve.
func (r *Resolver) resolveSymImport(pkgid uint, name string, sref *common.Symbol) bool {
	if sym, ok := r.Assemblers[pkgid].PackageRef.ImportFromNamespace(name); ok {
		ds := sref.DeclStatus
		*sref = *sym
		sref.DeclStatus = ds
		return true
	}

	return false
}
