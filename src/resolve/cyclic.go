package resolve

import (
	"math"

	"whirlwind/common"
	"whirlwind/syntax"
	"whirlwind/typing"
)

// Cyclic Symbol Resolution Algorithm
// ----------------------------------
// 1. Go through all the remaining definitions and collect a table of type
//    and interface definitions indicating which symbols they depend on
//    (efficiently selecting the relevant dependency relationships).
// 2. Iterate through all the definitions in the table and:
//    a. If the definition depends on a symbol which is not listed in the
//       table, log it as unresolved and remove it from the table and queue
//       of definitions.
//    b. If the definition only depends on symbols which are listed in the
//       table, consider it our operand definition and proceed to step 3.
// 3. Create an appropriate opaque type of the operand definition, then
//    temporarily remove it from the queue and perform a resolution pass
//    with it as a given.
// 4. Once the resolution pass is complete, attempt to resolve the operand
//    and store the result. Then, check what definitions remain and:
//    a. If no definitions remain, appropriately log the operand if it
//       fails to resolve and return the operand resolution result.
//    b. If only non-type/interf-definitions remain, mark all remaining
//       definitions as unresolved.  Then, appropriately log the operand
//       if it fails to resolve and return the operand resolution result
//    c. If some type definitions remain, proceed to step 5.
// 5. If the operand failed to resolve, add it to the back of the
//    definition queue.
// 6. Reset the state of the cyclic resolver (table, operand) and repeat
//    the algorithm from step 1.

// resolveCyclic provides a mechanism for resolving symbols that are cyclically
// dependent on each other as described in the algorithm above.
func (r *Resolver) resolveCyclic() bool {
	resolveSuccessful := true

resolveLoop:
	for true {
		table := r.buildCyclicDefTable()

		// if there is are no elements left, we can just exit
		if len(table) == 0 {
			break
		} else if len(table) == 1 {
			// if there is only one package left and only one element left in
			// that package, we know we can't resolve it.  It will be logged
			// later (at the end) -- so we can just set the `resolveSuccessful`
			// flag to false and break the resolution loop
			for _, pkgTable := range table {
				if len(pkgTable) == 1 {
					resolveSuccessful = false
					break resolveLoop
				}

				// only one table so no real need to break
			}
		}

		var operand *Definition
		var operandSrcPkgID uint
		operand, operandSrcPkgID, resolveSuccessful = r.getNextOperand(table)

		// if we can't find an operand, resolveSuccessful must be false and
		// resolution has failed so we break and log errors
		if operand == nil {
			break
		}

		r.createOpaqueType(operand, operandSrcPkgID)
		if r.resolveUnknowns() {
			if _, ok := r.resolveDef(operandSrcPkgID, operand); !ok {
				// if we fail to resolve, we just log unresolved and exit
				r.logUnresolvedDef(operandSrcPkgID, operand)
				return false
			}

			// everything resolved, we can just let the function return as
			// normal :)
			break
		} else {
			if _, ok := r.resolveDef(operandSrcPkgID, operand); !ok {
				// if the operand still fails to resolve, we add it back to the
				// queue at the back
				queue := r.Assemblers[operandSrcPkgID].DefQueue
				queue.Enqueue(operand)
				queue.Rotate()

				// the fatal error will be detected when the table only has one
				// remaining element -- we don't need to search for remaining
				// definitions here
			}
		}
	}

	// regardless of our resolution status, we should attempt to resolve all
	// remaining definitions (ie. not type definitions) and log any we can't
	// resolve.  Note that failure to resolve any of these definitions will
	// determine the final return flag of this function (ie. if one fails, the
	// flag is set to false regardless if all the others succeeded)
	for _, pa := range r.Assemblers {
		// since all of these definitions are non-dependent (meaning defining
		// one won't resolve any others at this stage), we can just loop through
		// them and attempt resolution sequentially
		for pa.DefQueue.Len() > 0 {
			curr := pa.DefQueue.Peek()
			if _, ok := r.resolveDef(pa.PackageRef.PackageID, curr); !ok {
				pa.logUnresolved(curr)
				resolveSuccessful = false
			}

			// whether we log or we resolve, we want to dequeue the definition
			pa.DefQueue.Dequeue()
		}
	}

	// if resolution was successful, we need to make sure to clear the shared
	// opaque symbol before proceeding (we just set the SrcPackageID to be -1
	// (max uint value) and the name to be "" to ensure it never matches) --
	// this is not actually "necessary"; more of a failsafe against my own
	// "scatter-brainedness"
	if resolveSuccessful {
		*r.sharedOpaqueSymbol = common.OpaqueSymbol{Name: "", SrcPackageID: math.MaxUint32}
	}

	return resolveSuccessful
}

// createOpaqueType takes a definition and creates and stores an appropriate
// opaque type for it.  It stores it as the opaque type in all the walkers.
func (r *Resolver) createOpaqueType(operand *Definition, operandSrcPkgID uint) {
	var generic, requiresRef bool

	typeRequiresRef := func(suffix *syntax.ASTBranch) bool {
		if suffix.Name == "newtype" {
			subSuffix := suffix.BranchAt(0)
			if subSuffix.Name == "struct_suffix" {
				// structs always require a reference
				return true
			}

			// algebraic types may require a reference if they only contain one
			// value -- same logic as for type sets
			return subSuffix.Len() > 1
		}

		// if the suffix is only length 2, then we have an alias not a type set
		// which means that a reference may be required (if this stores a struct
		// for example) and since we can't know until this type resolves, we
		// have to assume one is necessary
		return suffix.Len() == 2
	}

	switch operand.Branch.LeafAt(0).Kind {
	case syntax.TYPE:
		generic = operand.Branch.BranchAt(2).Name == "generic_tag"
		requiresRef = typeRequiresRef(operand.Branch.Last().(*syntax.ASTBranch))
	case syntax.INTERF:
		if _, ok := operand.Branch.Content[2].(*syntax.ASTBranch); ok {
			generic = ok
		}

		// interfaces never require a reference
	case syntax.CLOSED:
		generic = operand.Branch.BranchAt(3).Name == "generic_tag"
		requiresRef = typeRequiresRef(operand.Branch.Last().(*syntax.ASTBranch))
	}

	dependsOn := make(map[string][]uint)
	for name, unknown := range operand.Unknowns {
		var currPkgID uint
		if unknown.ForeignPackage == nil {
			currPkgID = operandSrcPkgID
		} else {
			currPkgID = unknown.ForeignPackage.PackageID
		}

		if pkgIDs, ok := dependsOn[name]; ok {
			dependsOn[name] = append(pkgIDs, currPkgID)
		} else {
			dependsOn[name] = []uint{currPkgID}
		}
	}

	var opaqueType typing.DataType
	if generic {
		opaqueType = &typing.OpaqueGenericType{}
	} else {
		opaqueType = &typing.OpaqueType{Name: operand.Name}
	}

	*r.sharedOpaqueSymbol = common.OpaqueSymbol{
		Name:         operand.Name,
		Type:         opaqueType,
		SrcPackageID: operandSrcPkgID,
		DependsOn:    dependsOn,
		RequiresRef:  requiresRef,
	}
}

// getNextOperand searches the table for the first valid operand and
// appropriately logs all symbols as unresolved as necessary.  If it finds an
// operand, it removes it from the queue and returns it.
func (r *Resolver) getNextOperand(table map[uint]map[string]*Definition) (*Definition, uint, bool) {
	encounteredUnresolved := false

	for currPkgTableID, pkgTable := range table {
		// fetch the queue that we are going to process (we need to use the
		// queue so that we can remove definitions as we look for the operand)
		pkgQueue := r.Assemblers[currPkgTableID].DefQueue

		// `mark` is the definition at the top of the queue, we know to exit the
		// loop when we hit this definition again
		mark := pkgQueue.Peek()

		// rotate the mark out of the front of the queue
		pkgQueue.Rotate()

		// begin iterating through the queue; skipping any non-type definitions
		// and pruning out all definition we encounter that won't ever be
		// resolveable until we encounter a valid operand
		var def *Definition
	pkgDefLoop:
		for def != mark {
			def = pkgQueue.Peek()

			// if it is not in our pkgTable then it is not a definition we need
			// to consider here so we can skip it
			if _, ok := pkgTable[def.Name]; !ok {
				pkgQueue.Rotate()
				continue
			}

			// processUnresolved processes an unresolved or unresolveable
			// definition and remove it from all the necessary resolution
			// queues.  This assumes that the current `def` is its argument
			processUnresolved := func() {
				// log it as unresolved
				r.logUnresolvedDef(currPkgTableID, def)

				// remove it from the queue and the resolution table (it
				// cannot be depended on or resolved from)
				pkgQueue.Dequeue()
				delete(pkgTable, def.Name)

				// the `Dequeue` call facilitates rotation so there is no need
				// to call rotate here -- we can just call this function and
				// progress to the next cycle of the loop
			}

			for _, unknown := range def.Unknowns {
				// it is stored locally
				if unknown.ForeignPackage == nil {
					// if it does not exist in the current package table, then
					// it can never be resolved
					if _, ok := pkgTable[unknown.Name]; !ok {
						processUnresolved()
						continue pkgDefLoop
					}
				} else if foreignTable, ok := table[unknown.ForeignPackage.PackageID]; ok {
					// if our symbol is from a package being resolved, then we
					// check to see if it is in that packages definition table
					// -- it can be resolved.  If it is not, then it won't ever
					// be resolved and we can consider it unresolveable.
					if _, ok := foreignTable[unknown.Name]; !ok {
						processUnresolved()
						continue pkgDefLoop
					}
				} else {
					// if we reach here, we know it is in an already resolved
					// package which implies that if it was not found, it won't
					// ever be and is resolvable
					processUnresolved()
					continue pkgDefLoop
				}
			}

			// if we reach here, then we know the definition can be an operand
			// so we remove it from the queue and return it to be processed
			pkgQueue.Dequeue()
			return def, currPkgTableID, encounteredUnresolved
		}
	}

	// if we can't find an operand, then all remaining definitions were invalid
	return nil, 0, false
}

// logUnresolvedDef logs a definitions as unresolved -- it is shorthand for
// a called to `PAssembler.logUnresolved` since this called is used so much
func (r *Resolver) logUnresolvedDef(pkgID uint, def *Definition) {
	r.Assemblers[pkgID].logUnresolved(def)
}

// buildCyclicDefTable constructs the table of the type and interface
// definitions and their dependencies for cyclic resolution (step 1).  This
// table is organized first by package to prevent name conflicts.
func (r *Resolver) buildCyclicDefTable() map[uint]map[string]*Definition {
	table := make(map[uint]map[string]*Definition)

	for _, pa := range r.Assemblers {
		if pa.DefQueue.Len() > 0 {
			// to iterate through the queue, we take a starting definition and
			// rotate through the queue until we hit it a second time
			start := pa.DefQueue.Peek()
			curr := start

			// guarantee at least one loop at the start since we know curr is
			// set to start at the very beginning
			for ok := true; ok; ok = curr != start {
				currBranch := curr.Branch

				// annotated definitions can contain type defs and interf defs;
				// we want to extract and examine the inner definition
				if currBranch.Name == "annotated_def" {
					currBranch = curr.Branch.BranchAt(curr.Branch.Len() - 1)
				}

				if currBranch.Name == "type_def" || currBranch.Name == "interf_def" {
					// we need to extract the name of the definition before we
					// can put it in the table.  NOTE: should `Name` by a field
					// of `Definition`?
					var name string
					if currBranch.LeafAt(1).Kind == syntax.TYPE {
						name = currBranch.LeafAt(2).Value
					} else {
						name = currBranch.LeafAt(1).Value
					}

					// we first need to find the package specific table or
					// create one if it doesn't already exist and then insert
					// our definition into that
					if pkgTable, ok := table[pa.PackageRef.PackageID]; ok {
						pkgTable[name] = curr
					} else {
						pkgTable := make(map[string]*Definition)
						pkgTable[name] = curr
						table[pa.PackageRef.PackageID] = pkgTable
					}
				}

				pa.DefQueue.Rotate()
				curr = pa.DefQueue.Peek()
			}
		}
	}

	return table
}
