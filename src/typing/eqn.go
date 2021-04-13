package typing

import "whirlwind/logging"

// UnknownType is an unknown in a type equation.  This is technically a DataType
// as it can be used in HIR data structures (as a placeholder) until it is
// evaluated.
type UnknownType struct {
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
	// Furthermore, no types in this field may be UnknownTypes.
	Constraints []DataType

	// SourceExpr stores the TypeExpression that produced this unknown if one
	// exists. This field is `nil` for values.
	SourceExpr TypeExpression

	// Potentials contains all the possible types that this unknown type could
	// be -- this is used during things like function calls where an unknown may
	// not be the first type is coerced to.  This field once unified could
	// propagated as a known type.  `coerceUnknowns` updates this field by
	// default
	Potentials []DataType

	// Position indicates wheter this type was defined so we can log errors if
	// it is not solvable
	Position *logging.TextPosition
}

func (ut *UnknownType) Repr() string {
	if ut.EvalType != nil {
		return ut.EvalType.Repr()
	}

	return "<unknown type>"
}

func (ut *UnknownType) addPotential(dt DataType) {
	ut.Potentials = append(ut.Potentials, dt)
}

// This function should never be used (logically)
func (*UnknownType) copyTemplate() DataType {
	logging.LogFatal("Type unknown used in generic template")
	return nil
}

// This function assumes that the evaluated type already exists; `InnerType`
// strips away the unknown shell (or errors)
func (ut *UnknownType) equals(other DataType) bool {
	return ut.EvalType.equals(other)
}

// TypeEquation represents a relation between one or more types that the solver
// can manipulate to solve for one or more type variables.
type TypeEquation struct {
	Lhs, Rhs TypeExpression

	// Unknowns stores the types that must be resolved for this equation to be
	// satisfied.  This is a map to prevent duplication
	Unknowns map[*UnknownType]struct{}
}

// TypeExpression is equivalent to an arithmetic expression used to create a a
// larger expression or equation in real numbers -- the key difference being
// type expressions apply to types and expressions of types instead of real
// numbers.  These types correspond to the "operators" of the Hindley-Milner
// type system.
type TypeExpression interface {
	// Result performs upward type deduction on the expression to determine the
	// resultant type.  If this type is unknown (deduction fails), the unknown
	// is returned and the return flag is set to false.  If the type was
	// deducible, then the deduced type is returned and the return flag is set
	// to true.
	Result() (DataType, bool)

	// Propagate performs downward type deduction: propagating an expected
	// result down to the leaves of the type expression.  The return flag
	// indicates whether propagation suceeded.  Note that the expression is
	// simplified (modified) as propagation occurs if this operation is
	// successful.
	Propagate(result DataType) bool
}

// TypeValueExpr is the simplest of the type expressions: it contains a
// single type value and exists as a leaf on a larger type expression.
type TypeValueExpr struct {
	// s is a reference to the parent solver
	s *Solver

	StoredType DataType
}

func (tve *TypeValueExpr) Result() (DataType, bool) {
	ut, isUnknown := tve.StoredType.(*UnknownType)

	if ut.EvalType != nil {
		return ut.EvalType, true
	}

	return tve.StoredType, !isUnknown
}

func (tve *TypeValueExpr) Propagate(dt DataType) bool {
	if ut, ok := dt.(*UnknownType); ok {
		if len(ut.Constraints) == 0 {
			tve.s.evaluate(ut, dt)
			return true
		}

		for _, cons := range ut.Constraints {
			if tve.s.CoerceTo(dt, cons) {
				tve.s.evaluate(ut, dt)
				return true
			}
		}

		return false
	}

	// if it is not unknown, propagation always succeeds
	return true
}

// TypeAppExpr represents a function application.  It assumes that the function
// call it contains is valid given the information known at the time of its
// creation -- all unknown types are properly constrained.
type TypeAppExpr struct {
	s *Solver

	Func *FuncType
	Args []DataType
}

func (tae *TypeAppExpr) Result() (DataType, bool) {
	if urt, ok := tae.Func.ReturnType.(*UnknownType); ok {
		for _, arg := range tae.Func.Args {
			if aut, ok := arg.Val.Type.(*UnknownType); ok {
				tae.s.inferFromPotentials(aut)
			}
		}

		if urt.EvalType != nil {
			return urt.EvalType, true
		}

		return urt, false
	}

	return tae.Func.ReturnType, true
}

func (tae *TypeAppExpr) Propagate(dt DataType) bool {
	// TODO: downward type deduction

	return false
}
