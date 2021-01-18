package resolve

// Cyclic Symbol Resolution Algorithm
// ----------------------------------
// 1. Go through all the remaining definitions and collect a table of type
//    definitions indicating which symbols they depend on (efficiently
//    selecting the relevant dependency relationships).
// 2. Iterate through all the type definitions in the table and:
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
//    b. If only non-type-definitions remain, mark all remaining
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
	// Code to Log Errors:
	// for _, pa := range r.Assemblers {
	// 	if pa.DefQueue.Len() > 0 {
	// 		pa.logUnresolved()
	// 	}
	// }

	return false
}
