package resolve

import "github.com/ComedicChimera/whirlwind/src/syntax"

// Cyclic Symbol Resolution Algorithm
// ----------------------------------
// 1. Go through all the remaining definitions and collect a table of type
//    and interface definitions indicating which symbols they depend on
//    (efficiently selecting the relevant dependency relationships).
// 2. Iterate through all the definitions in the table and:
//    a. If the definition depends on a symbol which is not listed in the
//       table, log it as unresolved and remove it from the table and list
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
			// that package, we know we can't resolve it and so we log
			// unresolved and exit resolution
			for pkgID, pkgTable := range table {
				if len(pkgTable) == 1 {
					// why do I have to loop to get the first element...
					for _, def := range pkgTable {
						r.logUnresolvedDef(pkgID, def)
					}

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

		// remove the operand from the definition queue
		queue := r.Assemblers[operandSrcPkgID].DefQueue
		for queue.Peek() != operand {
			queue.Rotate()
		}
		queue.Dequeue()

		r.createOpaqueType(operand)
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
				queue.Enqueue(operand)
				queue.Rotate()

				// the fatal error will be detected when the table only has one
				// remaining element -- we don't need to search for remaining
				// definitions here
			}
		}
	}

	// if we failed to resolve, we need to log all remaining definitions as
	// unresolved (all the non-type definitions that is)
	if !resolveSuccessful {
		for _, pa := range r.Assemblers {
			// empty out the queue as we log all remaining errors
			for pa.DefQueue.Len() > 0 {
				pa.logUnresolved(pa.DefQueue.Peek())
				pa.DefQueue.Dequeue()
			}
		}
	}

	return resolveSuccessful
}

// createOpaqueType takes a definition and creates and stores an appropriate
// opaque type for it.  It stores it as the opaque type in all the walkers.
func (r *Resolver) createOpaqueType(operand *Definition) {

}

// getNextOperand searches the table for the first valid operand and
// appropriately logs all symbols as unresolved as necessary
func (r *Resolver) getNextOperand(table map[uint]map[string]*Definition) (*Definition, uint, bool) {
	encounteredUnresolved := false

	for currPkgTableID, pkgTable := range table {
	pkgDefLoop:
		for _, def := range pkgTable {
		unknownLoop:
			for _, unknown := range def.Unknowns {
				// it is stored locally or brought in via a namespace import
				if unknown.ForeignPackage == nil {
					if _, ok := pkgTable[unknown.Name]; !ok {
						// if it is not stored locally, then we check for a
						// namespace import that is in one of our resolving
						// tables.  If it isn't in any of those, we know it
						// doesn't exist by the same logic as known imports
						for wimportID, wimport := range r.Assemblers[currPkgTableID].PackageRef.ImportTable {
							if wimport.NamespaceImport {
								if foreignTable, ok := table[wimportID]; ok {
									if _, ok := foreignTable[unknown.Name]; ok {
										continue unknownLoop
									} else {
										r.logUnresolvedDef(currPkgTableID, def)
										// TODO: remove from queue
										continue pkgDefLoop
									}
								}
							}
						}
					}
				} else if foreignTable, ok := table[unknown.ForeignPackage.PackageID]; ok {
					// if our symbol is from a package being resolved, then we
					// check to see if it is in that packages definition table
					// -- it can be resolved.  If it is not, then it won't ever
					// be resolved and we can consider it unresolveable.
					if _, ok := foreignTable[unknown.Name]; !ok {
						r.logUnresolvedDef(currPkgTableID, def)
						// TODO: remove from queue
						continue pkgDefLoop
					}
				} else {
					// if we reach here, we know it is in an already resolved
					// package which implies that if it was not found, it won't
					// ever be and is resolvable
					r.logUnresolvedDef(currPkgTableID, def)
					// TODO: remove from queue
					continue pkgDefLoop
				}
			}

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
			var curr *Definition

			for curr != start {
				curr = pa.DefQueue.Peek()
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
			}

		}
	}

	return table
}
