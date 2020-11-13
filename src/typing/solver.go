package typing

// Solver is the abstraction responsible for inferring the types of an given
// block or context.  It is also referred to as the inferencer.
type Solver struct {
	Equations []*TypeEquation
}
