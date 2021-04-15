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
	// satisfied.  This is a map to prevent duplication.  The boolean indicates
	// which side of the expression the unknown is on: false => rhs, true => lhs
	Unknowns map[*UnknownType]bool
}

// TypeExpression is equivalent to an arithmetic expression used to create a a
// larger expression or equation in real numbers -- the key difference being
// type expressions apply to types and expressions of types instead of real
// numbers.  These types correspond to the "operators" of the Hindley-Milner
// type system.  NOTE: The return values of both of the functions of this
// interface MUST indicate whether all sub-expressions are solveable.
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

	// Unknown manipulation/updating functions
	AddUnknown(unk *UnknownType)
	RemoveUnknown(unk *UnknownType)

	// Solveable indicates whether an expression can be fully solved
	Solveable() bool
}

// typeExprBase is the base structure for all type expressions.  It provides
// some functionality that works the same for all of them.
type typeExprBase struct {
	// s is a reference to the parent solver
	s *Solver

	// parentEqn is the parent equation of this expression
	parentEqn *TypeEquation

	// exprUnknowns is a list of all the unknowns of this expression
	exprUnknowns map[*UnknownType]struct{}
}

func (s *Solver) newTypeExprBase() typeExprBase {
	return typeExprBase{
		s:            s,
		parentEqn:    s.CurrentEquation,
		exprUnknowns: make(map[*UnknownType]struct{}),
	}
}

func (teb *typeExprBase) AddUnknown(unk *UnknownType) {
	teb.exprUnknowns[unk] = struct{}{}
}

func (teb *typeExprBase) RemoveUnknown(unk *UnknownType) {
	delete(teb.exprUnknowns, unk)
}

func (teb *typeExprBase) Solveable() bool {
	return len(teb.exprUnknowns) > 0
}

// evaluate evaluates an unknown type to a known type (final step of inference).
// Flag bool indicates if this evaluation solved all sub-expressions or not
func (teb *typeExprBase) evaluate(ut *UnknownType, dt DataType) bool {
	// update unknown type type
	ut.EvalType = dt
	ut.Potentials = nil

	// remove unknown from expression and equation
	delete(teb.parentEqn.Unknowns, ut)
	teb.RemoveUnknown(ut)

	// propagate the type down and attempt to simplify sub-expressions
	if ut.SourceExpr.Propagate(dt) {
		ut.SourceExpr = teb.s.newTypeValueExpr(ut.EvalType)
		return true
	}

	return false
}

// inferFromPotentials attempts to infer a known type from the potentials of an
// unknown type.  `Potentials` is cleared after this is called.
func (teb *typeExprBase) inferFromPotentials(ut *UnknownType) bool {
	if len(ut.Potentials) == 0 {
		return false
	}

	if udt, ok := teb.s.unify(ut.Potentials...); ok {
		if teb.s.matchConstraints(ut, udt) {
			teb.evaluate(ut, udt)

			return true
		}
	}

	ut.Potentials = nil
	return false
}

// inferInst attempts to deduce a type for an unknown based on its constraints.
// If there are no constaints or multiple constaints, then this fails.  If, however,
// these is only one constaint, then a protocol will be followed to attempt to determine
// the most sensible type (eg. `Integral` => `int` as default).
func (teb *typeExprBase) inferInst(ut *UnknownType) bool {
	return false
}

// TypeValueExpr is the simplest of the type expressions: it contains a
// single type value and exists as a leaf on a larger type expression.
type TypeValueExpr struct {
	typeExprBase

	StoredType DataType
}

func (s *Solver) newTypeValueExpr(st DataType) *TypeValueExpr {
	tve := &TypeValueExpr{
		typeExprBase: s.newTypeExprBase(),
		StoredType:   st,
	}

	if ut, ok := st.(*UnknownType); ok {
		tve.AddUnknown(ut)
	}

	return tve
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
			return tve.evaluate(ut, dt)
		}

		if tve.s.matchConstraints(ut, dt) {
			return tve.evaluate(ut, dt)
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
	typeExprBase

	Func *FuncType
	Args []DataType
}

func (s *Solver) newTypeAppExpr(fn *FuncType, args []DataType) *TypeAppExpr {
	tae := &TypeAppExpr{
		typeExprBase: s.newTypeExprBase(),
		Func:         fn,
		Args:         args,
	}

	// add all the unknowns to the expression once it is created
	for _, arg := range fn.Args {
		if ut, isUnknown := arg.Val.Type.(*UnknownType); isUnknown {
			tae.AddUnknown(ut)
		}
	}

	for _, argDt := range args {
		if ut, isUnknown := argDt.(*UnknownType); isUnknown {
			tae.AddUnknown(ut)
		}
	}

	if ut, isUnknown := fn.ReturnType.(*UnknownType); isUnknown {
		tae.AddUnknown(ut)
	}

	return tae
}

func (tae *TypeAppExpr) Result() (DataType, bool) {
	if urt, ok := tae.Func.ReturnType.(*UnknownType); ok {
		for _, arg := range tae.Func.Args {
			if aut, ok := arg.Val.Type.(*UnknownType); ok {
				tae.inferFromPotentials(aut)
			}
		}

		if urt.EvalType != nil {
			return urt.EvalType, tae.Solveable()
		}

		return urt, false
	}

	return tae.Func.ReturnType, tae.Solveable()
}

func (tae *TypeAppExpr) Propagate(dt DataType) bool {
	rt, ok := tae.Result()

	// not unknown; we can assume correctness of propagation
	if ok {
		return tae.Solveable()
	}

	urt := rt.(*UnknownType)
	if tae.s.matchConstraints(urt, dt) {
		return tae.evaluate(urt, dt) && tae.Solveable()
	}

	return false
}
