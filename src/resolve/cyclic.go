package resolve

// resolveCyclic runs stage 3 of the resolution algorithm: it attempts cyclic
// dependendency resolution on our top level definitions -- it is the "last
// resort" resolution pass.  It returns a boolean indicating whether all of the
// remaining type definitions will be resolved.  Regardless of this, all
// determinate definitions will be resolved by the time this function exits.
func (r *Resolver) resolveCyclic() bool {
	return false
}
