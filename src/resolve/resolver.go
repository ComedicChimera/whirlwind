package resolve

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/validate"
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
//    queue.  Pass over the current queue until a definition repeats with no additional
//    symbols declared.  Pass over as follows:
//    a. If the top definition is resolveable, finalize it and remove it from the queue.
//    b. If the top definition depends on symbols to be defined in another package, rotate
//       it to the back of its package's queue and switch the queue to that of the package
//       in which the first symbol is defined.
//    c. If the top definitions depends on symbols that may be local or namespace imported
//       rotate it to the back of queue.  Do not change the current queue.
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

	// step 3 -- resolution of unknowns -- if this step succeeds, we have no
	// errors to log and can just return true.
	if r.resolveUnknowns() {
		return true
	}

	// step 4 -- log appropriate errors, know there are errors so assume failure
	// status -- default return false.
	for _, pa := range r.Assemblers {
		if pa.DefQueue.Len() > 0 {
			pa.logUnresolved()
		}
	}

	return false
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

// resolveUnknowns runs the main portion of the resolution algorithm (step 3).
// Boolean return value indicates whether all unknowns were resolved (resolution
// succeeded) or not (resolution failed).
func (r *Resolver) resolveUnknowns() bool {
	// mark is used to store the definition that will be used to test for
	// repeats.  It is set to be the first definition, and if that definition is
	// encountered again with no additional defined symbols, then the evaluation
	// loop exits.  If it is encountered again and updated (new symbols), then
	// we simply keep going.  If it is encountered and resolved, we take the
	// mark to be the next item in the queue and continue.
	var mark *Definition

	// symbolsDefined is a flag used to indicate that some number of symbols were
	// defined in between the mark being set and being encountered again
	symbolsDefined := false

	// currPkg stores the current queue.  We initialize this arbitrarily to be
	// the first non-empty queue found in the resolution unit.
	var currQueue *DefinitionQueue
	var currPkgID uint // used to know which walker to use for resolution
	for _, pa := range r.Assemblers {
		if pa.DefQueue.Len() > 0 {
			currQueue = pa.DefQueue
			currPkgID = pa.PackageRef.PackageID
			break
		}
	}

	// if no queue was set, then they are all empty already so we can just return
	if currQueue == nil {
		return true
	}

	for {
		top := currQueue.Peek()

		// if we cannot readily resolve the top, we determine what to do in
		// terms of marking and current queue and if necessary, rotate the top
		// definition to the back of its queue.
		if unknown, resolved := r.resolveDef(currPkgID, top); !resolved {
			// set the mark if it is nil => need new mark
			if mark == nil {
				mark = top

				// clear symbols flag (new mark)
				symbolsDefined = false
			} else if mark == top {
				// if we are encountering the same mark twice, check if any new
				// symbols were defined.  If so, continue.  Otherwise,
				// resolution has failed and we return.
				if symbolsDefined {
					// clear the flag
					symbolsDefined = false
				} else {
					return false
				}
			}

			// rotate the top to the back (doesn't change the reference)
			currQueue.Rotate()

			// symbol located in a foreign package, need to update the queue to
			// be that of the foreign package (switch resolution targets)
			if unknown.ForeignPackage != nil {
				currPkgID = unknown.ForeignPackage.PackageID
				currQueue = r.Assemblers[currPkgID].DefQueue
			} else {
				// we don't need to check if the current queue is empty b/c we
				// know there is at least one symbol still in it (the current
				// unknown definition)
				continue
			}
		} else {
			// if the top is equal to the mark, we clear the mark
			if top == mark {
				mark = nil
			}

			// we have defined a symbol, so we set the flag appropriately
			symbolsDefined = true

			// the definition has been finalized, so we remove it for the
			// current queue (no need to resolve it anymore)
			currQueue.Dequeue()
		}

		// if the current queue is empty, we switch to the first new queue of
		// length greater than one.  If there are no such queues, resolution has
		// finished and we return
		if currQueue.Len() == 0 {
			for _, pa := range r.Assemblers {
				if pa.DefQueue.Len() > 0 {
					currQueue = pa.DefQueue
					currPkgID = pa.PackageRef.PackageID
					break
				}
			}

			// if the current queue's length is still 0, then there no new queue
			// was found
			if currQueue == nil {
				return true
			}
		}
	}
}

// resolveDef attempts to resolve and finalize a definition
func (r *Resolver) resolveDef(pkgid uint, def *Definition) (*validate.UnknownSymbol, bool) {
	w := r.Assemblers[pkgid].Walkers[def.SrcFile]

	// go through and update the unknowns (remove those that have been accounted
	// for) and determine which unknown should be returned (if any) -- stored in
	// funknown
	var funknown *validate.UnknownSymbol
	for name, unknown := range def.Unknowns {
		if _, ok := w.Lookup(name); ok {
			delete(def.Unknowns, name)
		} else if funknown == nil {
			funknown = unknown
		}
	}

	// if there is no unknown to return, then we can resolve the definition
	// because all the unknowns were defined.
	if funknown == nil {
		// this should always succeed if it was not eliminated in the initial
		// pass -- all unknowns are defined; definition should be well-formed
		hirn, _, _ := w.WalkDef(def.Branch)
		def.SrcFile.AddNode(hirn)
		return nil, true
	}

	return funknown, false
}
