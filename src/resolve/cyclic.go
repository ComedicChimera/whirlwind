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
	for true {
		table := r.buildCyclicDefTable()
		_ = table
	}

	// Code to Log Errors:
	// for _, pa := range r.Assemblers {
	// 	if pa.DefQueue.Len() > 0 {
	// 		pa.logUnresolved()
	// 	}
	// }

	return false
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
