package resolve

import (
	"whirlwind/common"
	"whirlwind/logging"
)

// The Resolution Algorithm
// ------------------------
// 1. Go through every definition in the resolution unit and extract every type
//    or interface definition in those packages and determine if they depend on any
//    unknown symbols.  If they do, add them to the resolution queue and table; if
//    they don't automatically define them.  We want to isolate these two kinds of
//    definitions since at the top level, they are the only definitions that can
//    cause other definitions to resolve (eg. a function definition can never be
//    referenced directly as a type which at the top level is the only kind of
//    definition interdependency).  These definitions are known as "determinate
//    definitions".
// 2. Select an arbitrary package to be the start of our resolution.  Begin
//    resolution with that specific package's definition queue considering
//    it to be the current queue.  Pass over the current queue until a definition
//    repeats with no additional symbols declared.  Pass over as follows:
//    a. If the top definition is resolveable, finalize it and remove it from the queue.
//    b. If the top definition depends on symbols to be defined in another package, rotate
//       it to the back of its package's queue and switch the queue to that of the package
//       in which the first symbol is defined.
//    c. If the top definitions depends on symbols that may be local
//       rotate it to the back of queue.  Do not change the current queue.
// 3. Attempt to resolve all cyclically defined symbols.  Use the following
//    algorithm to do so:
//    a. Take the first definition in the queue to be the "operand" and remove
//    it from the queue.
//    b.
// 4. Resolve all remaining non-determinate definitions and log all unresolved
//    imports (explicit -- after definitions).

// Resolver is main construct responsible for symbol resolution and high-level
// package assembly (it implements the resolution algorithm described above). It
// operates on entire resolution units as opposed to individual packages.
type Resolver struct {
	// assemblers stores a list of all the package assemblers for this
	// resolution unit
	assemblers map[uint]*PAssembler

	// depGraph is the dependency graph constructed by the compiler
	depGraph map[uint]*common.WhirlPackage
}

// NewResolver creates a new resolver for the given set of packages
func NewResolver(pkgs []*common.WhirlPackage, depg map[uint]*common.WhirlPackage) *Resolver {
	r := &Resolver{
		assemblers: make(map[uint]*PAssembler),
		depGraph:   depg,
	}

	for _, pkg := range pkgs {
		r.assemblers[pkg.PackageID] = &PAssembler{
			SrcPackage: pkg,
		}
	}

	return r
}

// Resolve runs the main resolution algorithm on all the packages in resolution
func (r *Resolver) Resolve() bool {
	allResolved := true
	for _, pa := range r.assemblers {
		allResolved = allResolved && pa.initialPass()
	}

	if allResolved {
		return true
	}

	// if standard resolution works, then we can just skip cyclic
	if r.resolveStandard() {
		// make sure to resolve all the other definitions
		r.resolveRemaining()
		return logging.ShouldProceed()
	}

	// standard resolution failed, need to use cyclic resolution
	if r.resolveCyclic() {
		// make sure to resolve all the other definitions
		r.resolveRemaining()
		return logging.ShouldProceed()
	}

	// cyclic resolution failed; return
	return false
}

// resolveStandard runs stage 2 of the resolution algorithm in which non-cyclic
// dependent definitions are resolved.  It returns `true` if all definitions
// resolved
func (r *Resolver) resolveStandard() bool {
	// go through each package assembler with a non-empty definition queue and
	// add it to a map of queues by package.  As long as this map is nonempty,
	// there are still queues to processed.  Once a queue has been processed it
	// should be removed.  Since the current queue switches "randomly" during
	// resolution, we can't just use a list to store them and slice off the
	// front for each one we process.
	unprocessedQueues := make(map[uint]*DefinitionQueue)
	for _, pa := range r.assemblers {
		if pa.DefQueue.Len() > 0 {
			unprocessedQueues[pa.SrcPackage.PackageID] = pa.DefQueue
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
		if dep, resolved := r.resolveDef(currPkgID, top); !resolved {
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
			if dep.ForeignPackage != nil {
				// switch only if we actually have a queue to switch to
				if _, ok := unprocessedQueues[currPkgID]; ok {
					currPkgID = dep.ForeignPackage.PackageID
					currQueue = r.assemblers[currPkgID].DefQueue
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

// resolveRemaining implements stage 4 of the resolution algorithm: it resolves
// all indeterminate definitions (functions, variables, etc.) and handles
// unresolved imports
func (r *Resolver) resolveRemaining() {
	for _, pa := range r.assemblers {
		pa.finalPass()
	}

	// only after all definitions have been resolved and final passes have
	// occurred are we good to check imports
	for _, pa := range r.assemblers {
		pa.checkImports()
	}
}

// resolveDef attempts to resolve and finalize a definition.  The returned
// symbol is the first symbol that was still undefined
func (r *Resolver) resolveDef(pkgid uint, def *Definition) (*DependentSymbol, bool) {
	// go through and update the dependents (remove those that have been accounted
	// for) and determine which dependent should be returned (if any) -- stored in
	// fdep
	var fdep *DependentSymbol
	for name, dep := range def.Dependents {
		if _, ok := r.lookup(pkgid, def.SrcFile, dep); ok {
			delete(def.Dependents, name)
		} else if fdep == nil {
			fdep = dep
		}
	}

	// if there is no unknown to return, then we can resolve the definition
	// because all the unknowns were defined.
	if fdep == nil {
		r.assemblers[pkgid].walkDef(def.SrcFile, def.Branch, def.DeclStatus)
		return nil, true
	}

	return fdep, false
}

// lookup attempts to find a defined symbol that matches a given dependent
// symbol.  `wfile` should be the file of the definition that contains the
// dependent, not the dependent itself.  This function will update local symbol
// imports as necessary
func (r *Resolver) lookup(pkgid uint, wfile *common.WhirlFile, dep *DependentSymbol) (*common.Symbol, bool) {
	if dep.ForeignPackage != nil {
		// if it is in a foreign package, then we need to look it up there
		if sym, ok := dep.ForeignPackage.ImportFromNamespace(dep.Name); ok {
			// if it is not an implicit import then we may need to update the
			// symbol import with new symbol definition
			if !dep.ImplicitImport {
				// this whirl symbol import must match the dependency since them
				// matching is literally the only way it could be created
				wsi := wfile.LocalTable[dep.Name]

				// symbol ref is unresolved if name == ""
				if wsi.SymbolRef.Name == "" {
					*wsi.SymbolRef = *sym
				}
			}

			return sym, true
		}
	} else if sym, ok := r.assemblers[pkgid].SrcPackage.GlobalTable[dep.Name]; ok {
		// must be a symbol defined in the global namespace
		return sym, true
	}

	// TODO: opaque symbol handling?

	return nil, false
}
