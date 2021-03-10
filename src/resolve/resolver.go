package resolve

import (
	"whirlwind/common"
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
//    c. If the top definitions depends on symbols that may be local
//       rotate it to the back of queue.  Do not change the current queue.
// 4. If any definitions remain, perform single file cyclic resolution (as
//    described in `cyclic.go`) on them.  This algorithm will log all unresolveable symbols
//    appropriately so we have no need for any further processing.

// Resolver is the main abstraction for package resolution. It is responsible
// for resolving all of the symbols in a single package as well as those
// shared/cross-referenced by multiple packages.  The resolver operates in
// resolution units wherein each unit is composed of one or more packages that
// are mutually dependent.  It facilitates the Resolution Algorithm.
type Resolver struct {
	Assemblers map[uint]*PAssembler

	// depGraph stores the dependency graph for the resolver so that it can
	// lookup known symbols
	depGraph map[uint]*common.WhirlPackage

	// sharedOpaqueSymbol stores a common opaque symbol reference to be given to
	// all package assemblers to share with all of their walkers.  It used
	// during cyclic dependency resolution.
	sharedOpaqueSymbol *common.OpaqueSymbol
}

// NewResolver creates a new resolver for the given group of packages
func NewResolver(pkgs []*common.WhirlPackage, depG map[uint]*common.WhirlPackage) *Resolver {
	r := &Resolver{
		Assemblers:         make(map[uint]*PAssembler),
		depGraph:           depG,
		sharedOpaqueSymbol: &common.OpaqueSymbol{},
	}

	for _, pkg := range pkgs {
		r.Assemblers[pkg.PackageID] = NewPackageAssembler(pkg, r.sharedOpaqueSymbol)
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

	// step 4 -- perform cyclic resolution and log appropriate errors
	return r.resolveCyclic()
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
					if !r.resolveKnownSymImport(pkgid, name, sref) {
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

// resolveKnownSymImport attempts to resolve an explicitly-imported symbol from
// another package not in the current resolution unit with a given symbol
// reference. This function does not log an error if the symbol does not
// resolve.
func (r *Resolver) resolveKnownSymImport(pkgid uint, name string, sref *common.Symbol) bool {
	// import from the depGraph since any packages that is not in the current
	// resolution unit will be here (already processed)
	if sym, ok := r.depGraph[pkgid].ImportFromNamespace(name); ok {
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
	// go through each package assembler with a non-empty definition queue and
	// add it to a map of queues by package.  As long as this map is nonempty,
	// there are still queues to processed.  Once a queue has been processed it
	// should be removed.  Since the current queue switches "randomly" during
	// resolution, we can't just use a list to store them and slice off the
	// front for each one we process.
	unprocessedQueues := make(map[uint]*DefinitionQueue)
	for _, pa := range r.Assemblers {
		if pa.DefQueue.Len() > 0 {
			unprocessedQueues[pa.PackageRef.PackageID] = pa.DefQueue
		}
	}

	// if there are only empty queues, then resolution automatically succeeds
	if len(unprocessedQueues) == 0 {
		return true
	}

	// symbolsDefined is a flag used to indicate that some number of symbols were
	// defined in between the mark being set and being encountered again
	symbolsDefined := false

	// resolutionSucceeded is a flag used to indicate whether or not resolution
	// succeeded -- we have to set a flag instead of just returning so that we
	// ensure are queues are processed
	resolutionSucceeded := true

	// currQueue and currPkgID stores the queue and package ID of the assembler
	// currently being processed.  This may switch without a queue being removed
	// from `unprocessedQueues` if a foreign package is encountered.
	var currQueue *DefinitionQueue
	var currPkgID uint

	// nextQueue automatically fetches the next available queue for processing
	// from `unprocessedQueues` and stores it into `currQueue` (and updated
	// `currPkgID`).  It returns `false` if no queues remain.
	nextQueue := func() bool {
		for pid, queue := range unprocessedQueues {
			currPkgID = pid
			currQueue = queue
			return true
		}

		return false
	}

	// call nextQueue to initialize `currQueue` and `currPkgID`
	nextQueue()

	// mark is used to store the definition that will be used to test for
	// repeats.  It is set to be the first definition, and if that definition is
	// encountered again with no additional defined symbols, then the evaluation
	// loop exits.  If it is encountered again and updated (new symbols), then
	// we simply keep going.  If it is encountered and resolved, we take the
	// mark to be the next item in the queue and continue.
	var mark *Definition
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
				// resolution on this queue has failed
				if symbolsDefined {
					// clear the flag
					symbolsDefined = false
				} else {
					// set our resolution flag to indicate failure
					resolutionSucceeded = false

					// remove the queue we processed
					delete(unprocessedQueues, currPkgID)

					// select the next queue or stop resolution if no queues
					// remain
					if !nextQueue() {
						break
					}
				}
			}

			// rotate the top to the back (doesn't change the reference)
			currQueue.Rotate()

			// symbol located in a foreign package, need to update the queue to
			// be that of the foreign package (switch resolution targets)
			if unknown.ForeignPackage != nil {
				// switch only if we actually have a queue to switch to
				if _, ok := unprocessedQueues[currPkgID]; ok {
					currPkgID = unknown.ForeignPackage.PackageID
					currQueue = r.Assemblers[currPkgID].DefQueue
				} else {
					// otherwise, the foreign symbol will not be resolveable in
					// this pass (because its corresponding queue has already
					// been processed (or its package if its not in the current
					// resolution unit) and so no new match can be found this
					// pass.  Thus, we need to indicate that resolution failed
					resolutionSucceeded = false
				}

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

		// if the current queue is empty, we have finished processing all the
		// definitions in this queue successfully.  Thus, we need to either
		// switch to the next unprocessed queue or stop resolution of such
		// queues remain
		if currQueue.Len() == 0 {
			// remove the current queue from `unprocessedQueues` as it has now
			// been processed
			delete(unprocessedQueues, currPkgID)

			// select the next queue from the front of unprocessed
			// assemblers.  Or, if none remain, break to stop resolution
			if !nextQueue() {
				break
			}
		}
	}

	return resolutionSucceeded
}

// resolveDef attempts to resolve and finalize a definition
func (r *Resolver) resolveDef(pkgid uint, def *Definition) (*common.UnknownSymbol, bool) {
	w := r.Assemblers[pkgid].Walkers[def.SrcFile]

	// go through and update the unknowns (remove those that have been accounted
	// for) and determine which unknown should be returned (if any) -- stored in
	// funknown
	var funknown *common.UnknownSymbol
	for name, unknown := range def.Unknowns {
		if _, _, ok := w.Lookup(name); ok {
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
		hirn, _, _, _ := w.WalkDef(def.Branch)
		def.SrcFile.AddNode(hirn)
		return nil, true
	}

	return funknown, false
}
