package typing

import "whirlwind/logging"

// TypeEquation represents a relation between one or more types that the solver
// can manipulate to solve for one or more type variables.
type TypeEquation struct {
	Lhs, Rhs TypeExpression

	// Unknowns stores the types that must be resolved for this equation to be
	// satisfied.  This is a map to prevent duplication
	Unknowns map[*TypeUnknown]struct{}
}

// TODO: do not implement any of these until we know exactly how the solver is
// going to work -- I don't want to spend time messing with the API until I know
// exactly what I am going to need (maybe have expressions do all of the heavy
// lifting and the solver just orchestrate them or have the solver do
// everything)

// TypeExpression is equivalent to an arithmetic expression used to create a a
// larger expression or equation in real numbers -- the key difference being
// type expressions apply to types and expressions of types instead of real
// numbers.  These types correspond to the "operators" of the Hindley-Milner
// type system.
type TypeExpression interface {
	// Deduce applies downward type deduction to the expression to find the
	// secondary arguments from the primary argument (eg. for App(f, t1, t2,
	// ..., tn), this function finds t1, t2, ... tn from f)
	Deduce() bool

	// Inverse applies downward type deduction to the expression to find the
	// primary argument from the secondary argument (eg. for App(f, t1, t2, ...,
	// tn), this function find f from t1, t2, ..., tn)
	Inverse(args ...TypeExpression) bool

	// Result performs upward type deduction on the expression to determine the
	// resultant type (eg. the return type of App(f, t1, t2, ..., tn)).  It
	// returns a flag indicating whether deduction succeeded.
	Result() (DataType, bool)
}

// TypeUnknown is an unknown in a type equation.  This is technically a DataType
// as it can be used in HIR data structures (as a placeholder) until it is
// evaluated.
type TypeUnknown struct {
	// EvalType is the type reference shared by all knowns that is determined
	// when this type is inferred/evaluated.  This field is `nil` until it is
	// inferred.
	EvalType DataType

	// Constaints stores all the conditions that determine what this type could
	// be.  For example, when we create an integer literal, the `Integral` typeset
	// is added as a condition because whatever this type is evaluated to be must
	// be within the integral type set.  Similarly, if this is a type parameter,
	// then these will be the type restrictors -- of which it can be any.  Note that
	// the unknown will not be forced to meet all constraints -- just one of them.
	// Furthermore, no types in this field may be TypeUnknowns.
	Constraints []DataType

	// SourceExpr stores the TypeExpression that produced this unknown if one
	// exists. This field is `nil` for values.
	SourceExpr TypeExpression

	// Position indicates wheter this type was defined so we can log errors if
	// it is not solvable
	Position *logging.TextPosition
}

// InferInst attempts to deduce a type for this unknown based on its constraints.
// If there are no constaints or multiple constaints, then this fails.  If, however,
// these is only one constaint, then a protocol will be followed to attempt to determine
// the most sensible type (eg. `Integral` => `int` as default).
func (tu *TypeUnknown) InferInst() bool {
	return false
}

func (tu *TypeUnknown) Repr() string {
	if tu.EvalType != nil {
		return tu.EvalType.Repr()
	}

	return "<unknown type>"
}

// This function should never be used (logically)
func (tu *TypeUnknown) copyTemplate() DataType {
	logging.LogFatal("Type unknown used in generic template")
	return nil
}

// This function assumes that the evaluated type already exists; `InnerType`
// strips away the unknown shell (or errors)
func (tu *TypeUnknown) equals(other DataType) bool {
	return tu.EvalType.equals(other)
}

// SolvedExpr is the simplest of the type expressions: it indicates an
// expression that has been solved to a known type -- it is generated whenever
// the solver isn't sure if a side of an equation need be solved.  It's
// `TypeExpression` method implementations are trivial
type SolvedExpr struct {
	ResultType DataType
}

func (se *SolvedExpr) Result() (DataType, bool) {
	return se.ResultType, true
}

func (se *SolvedExpr) Deduce() bool {
	return true
}

func (se *SolvedExpr) Inverse(args ...TypeExpression) bool {
	// TODO: log fatal if this is called?
	return true
}

// App(f, t1, t2, ..., tn)
// .Deduce() (find t1, t2, ..., tn from f)
// .Inverse() (find f from t1, t2, ..., tn from f)
