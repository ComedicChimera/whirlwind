package typing

// TypeEquation represents a relation between one or more types that the solver
// can manipulate to solve for one or more type variables.
type TypeEquation struct {
	Lhs, Rhs TypeExpression
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
	// resultant type (eg. the return type of App(f, t1, t2, ..., tn)).  If this
	// function fails, it returns the unknowns that prevented deduction.
	Result() (DataType, []*TypeUnknown)
}

// TypeUnknown is an unknown in a type equation
type TypeUnknown struct {
	// TODO
}

// App(f, t1, t2, ..., tn)
// .Deduce() (find t1, t2, ..., tn from f)
// .Inverse() (find f from t1, t2, ..., tn from f)
