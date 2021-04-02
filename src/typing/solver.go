package typing

import "whirlwind/logging"

// The Solving Algorithm
// ---------------------
// TODO

// NOTE: All definitions relevant specifically to the solver are defined in
// `eqn.go` (except for the solver itself)

// Solver is a state machine used to keep track of relevant information as we
// process complex typing information.  It works both to deduce types up the
// tree (ie. `int + int => int`) and down the tree (eg. solving for the types of
// lambda arguments).  It is the primary mechanism by which all type deduction
// in Whirlwind occurs.  It's behavior/algorithm is described at the top of this
// file. It is also referred to as the inferencer.
type Solver struct {
	// Context is the log context for this solver
	Context *logging.LogContext

	// UnsolvedEquations is a list of the active/unsolved type equations
	UnsolvedEquations []*TypeEquation

	// GlobalBindings is a reference to all of the bindings declared at a global
	// level in the current package
	GlobalBindings *BindingRegistry

	// LocalBindings is a reference to all of the bindings that are imported
	// from other packages are only visible in the current file
	LocalBindings *BindingRegistry

	// CurrentEquation is the equation that is currently being built
	CurrentEquation *TypeEquation

	// CurrentExpr is the type expression currently being built by the solver.
	// This can correspond either to the LHS or RHS of `CurrentEquation`.  It
	// will be moved into its correct position `FinishExpr` is called.
	CurrentExpr TypeExpression
}

// CreateUnknown creates a new type unknown assuming that no expression already
// exists; viz. it creates a new unknown corresponding to an unknown type value
func (s *Solver) CreateUnknown(pos *logging.TextPosition, constaints ...DataType) *TypeUnknown {
	tu := &TypeUnknown{Position: pos}

	if s.CurrentEquation == nil {
		s.initEqn(tu)
	} else {
		s.CurrentEquation.Unknowns[tu] = struct{}{}
	}

	return tu
}

// initEqn initializes the current type equation with a new unknown
func (s *Solver) initEqn(tu *TypeUnknown) {
	s.CurrentEquation = &TypeEquation{
		Unknowns: map[*TypeUnknown]struct{}{tu: {}},
	}
}

// FinishExpr moves the `CurrentExpression` into its appropriate position in
// `CurrentEquation`. It will attempt to infer a known resultant type of the
// expression before moving it -- if it can it will push that simplified result
// instead of the full `CurrentExpression`.  It also requires a resultant type
// to provided (this can be an unknown) in case no expression has actually been
// built -- this will only be used in this case.
func (s *Solver) FinishExpr(resultant DataType) {
	if s.CurrentExpr == nil {
		s.CurrentExpr = &SolvedExpr{ResultType: resultant}
	} else if dt, ok := s.CurrentExpr.Result(); ok {
		s.CurrentExpr = &SolvedExpr{ResultType: dt}
	}

	s.pushExpr()
}

// pushExpr pushes the `CurrentExpression` into the `CurrentEquation`
func (s *Solver) pushExpr() {
	// if there is no current equation, we still want to push an rhs expr (in
	// case we need to infer the lhs).  If this is the case, then we know there
	// are no unknowns (`CreateUnknown` never called in current expression)
	if s.CurrentEquation == nil {
		s.CurrentEquation = &TypeEquation{}
	}

	if s.CurrentEquation.Rhs == nil {
		s.CurrentEquation.Rhs = s.CurrentExpr
	} else {
		s.CurrentEquation.Lhs = s.CurrentExpr
	}

	s.CurrentExpr = nil
}

// FinishEqn attempts to solve the current equation.  If it can, it return true.
// If it cannot, then it returns false and moves the current equation into the
// pool of unsolved equations
func (s *Solver) FinishEqn() bool {
	if s.solve(s.CurrentEquation) {
		s.CurrentEquation = nil
		return true
	}

	s.UnsolvedEquations = append(s.UnsolvedEquations, s.CurrentEquation)
	s.CurrentEquation = nil
	return false
}

// SolveAll attempts to solve all remaining type equations.  It does not clear
// the pool of unsolved equations (so that logging can occur in `Walker`)
func (s *Solver) SolveAll() bool {
	solvedAll := true
	for _, eqn := range s.UnsolvedEquations {
		solvedAll = s.solve(eqn)
	}

	return solvedAll
}

// solve attempts to solve a given type equation
func (s *Solver) solve(te *TypeEquation) bool {
	return false
}
