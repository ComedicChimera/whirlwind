package validate

import "whirlwind/common"

// PredicateValidator is a data structure that implements Stage 3 of compilation
// -- predicate validation.  It validates all expressions and function bodies --
// also handling generic evaluation.
type PredicateValidator struct {
	walkers []*Walker
}

func NewPredicateValidator(walkers map[*common.WhirlFile]*Walker) *PredicateValidator {
	pv := &PredicateValidator{}

	for _, w := range walkers {
		pv.walkers = append(pv.walkers, w)
	}

	return pv
}

// Validate runs the predicate validation algorithm on the given package
func (pv *PredicateValidator) Validate() {

}
