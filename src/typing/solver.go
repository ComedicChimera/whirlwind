package typing

import (
	"fmt"
	"whirlwind/logging"
)

// Solver is a state machine used to keep track of relevant information as we
// process complex typing information.  It works both to deduce types up the
// tree (ie. `int + int => int`) and down the tree (eg. solving for the types of
// lambda arguments).  It is the primary mechanism by which all type deduction
// in Whirlwind occurs.  The deduction algorithm itself is based on
// Hindley-Milner type inference -- it works by accumulating and unifying a set
// of constraints to determine values for all unknown types in a type context
// (eg. a function scope, an initializer, etc.).  It works very closely with the
// walker which acts to coordinate type deduction by supplying it information
// about the program.  Note that the solver does not handle any actual traversal
// of the AST -- the walker feeds it appropriate information about the types in
// the tree.
type Solver struct {
	// Context is the log context for this solver
	Context *logging.LogContext

	// GlobalBindings is a reference to all of the bindings declared at a global
	// level in the current package
	GlobalBindings *BindingRegistry

	// LocalBindings is a reference to all of the bindings that are imported
	// from other packages are only visible in the current file
	LocalBindings *BindingRegistry

	// Variables is the list of active type variables to be unified
	Variables map[int]*TypeVariable

	// Constraints is the list of constraints in the current type context
	Constraints []*TypeConstraint

	// Substitutions contains a map of the type substitutions that the solver is
	// using to "solve" the current type context.  These are essentially its
	// "guesses" as to what an unknown type should be.
	Substitutions map[int]*TypeSubstitution
}

// NewSolver creates a new solver with the given context and binding registries.
func NewSolver(ctx *logging.LogContext, lb, gb *BindingRegistry) *Solver {
	return &Solver{
		Context:        ctx,
		GlobalBindings: gb,
		LocalBindings:  lb,
		Variables:      make(map[int]*TypeVariable),
		Substitutions:  make(map[int]*TypeSubstitution),
	}
}

// TypeVariable represents a type variable in a type equation.
type TypeVariable struct {
	// ID is a unique integer value that identifies this variable in the current
	// type context.  It is primarily used so that other things can reference
	// this type variable in the solver (without going through a reference).
	ID int

	// DefaultType is the type this variable should be reduced to if no more
	// specific type is inferred from the constraints.  For example, when
	// integer literals are created, they are created as type variables with a
	// `Numeric` constraint, but they can be reduced to `int` should it be
	// reasonable for that to occur.  This field will be `nil` if no default
	// type exists.
	DefaultType DataType

	// LogUnsolvable is this type variable's error handler that is called if it
	// can't be solved.  This may change for different type variables -- this
	// allows the walker to specify error handling behavior
	LogUnsolvable func()

	// Unknown contains a reference to this type variable's corresponding
	// unknown type
	Unknown *UnknownType
}

// TypeConstraint is a data type representing a constraint equation
type TypeConstraint struct {
	// The data types on the left or right hand side can be constraint types,
	// concrete types, unknowns, or any combination of those things.
	Lhs, Rhs DataType

	// Kind indicates how the solver should treat this constraint during
	// unification: different constraints require different kinds of unification
	// (eg. type parameters must be exactly equal in a generic)
	Kind int

	Position *logging.TextPosition
}

// TypeSubstitution is a structure representing a type substitution made by the
// solver. It determines how substitution should occur if multiple constraints
// are applied to a type (as often are).
type TypeSubstitution struct {
	// SubbedType is the type that is currently being substituted
	SubbedType DataType

	// ConsKind indicates what kind of constraint applied this substitution.
	// This is used to determine what substitutions should be used when two are
	// suggested/in conflict.  This field assumes that this was a "left
	// substitution"; that is a substitution where the constraint would apply as
	// if this were the lhs type (since these constraints are directional)
	ConsKind int
}

// Type constraint kinds
const (
	TCEquality = iota // Lhs and Rhs must be exactly equal
	TCSubset          // Rhs must be coercible to Lhs
	TCSuperset        // Lhs must be coercible to Rhs
	TCCast            // Rhs must be castable to Lhs
)

// UnknownType is a type that occurs in a code that is a placeholder for a type
// variable. This is distinct from a WildcardType which is only used to
// facilitate parametric types.  UnknownTypes are meant for use during solving.
type UnknownType struct {
	// TypeVarID corresponds to the TypeVarID of the type variable in the
	// current type context
	TypeVarID int

	// EvalType is the type determined for this unknown type (by unifying the
	// constraints). It is `nil` until the corresponding type variable is
	// unified.  This should be an `InnerType`!
	EvalType DataType
}

func (ut *UnknownType) Repr() string {
	if ut.EvalType == nil {
		return "_"
	}

	return ut.EvalType.Repr()
}

func (ut *UnknownType) equals(other DataType) bool {
	// This is only called directly for unevaluated unknowns
	return true
}

func (ut *UnknownType) copyTemplate() DataType {
	// This method need not be anything more than an identity since unknowns can
	// ever occur inside generics as something for which a "copyTemplate" would
	// be required.
	return ut
}

// -----------------------------------------------------------------------------

// NewTypeVar creates a new type variable and adds it to the type context.  An
// initial constraint can also be passed to be added to the solver.  It returns
// an unknown type referencing the newly added type variable.  Both the
// `defaultType` and `initialConstraint` arguments can be `nil` if there is no
// known value for them.  `initialConsKind` should be set if `initialConstraint`
// is; if it isn't, `initialConsKind` should be -1.  It accepts a text position
// to that it can be added to the constraint (to log type mismatches)
func (s *Solver) NewTypeVar(defaultType DataType, pos *logging.TextPosition, handler func(), initialConstraint DataType, initialConsKind int) *UnknownType {
	tv := &TypeVariable{
		ID:            len(s.Variables),
		DefaultType:   defaultType,
		LogUnsolvable: handler,
	}

	s.Variables[tv.ID] = tv

	ut := &UnknownType{TypeVarID: tv.ID}
	if initialConstraint != nil {
		s.AddConstraint(ut, initialConstraint, initialConsKind, pos)
	}

	return ut
}

// AddConstraint adds a new constraint to the given context.
func (s *Solver) AddConstraint(lhs, rhs DataType, consKind int, pos *logging.TextPosition) {
	s.Constraints = append(s.Constraints, &TypeConstraint{
		Lhs: lhs, Rhs: rhs, Kind: consKind, Position: pos,
	})
}

// Solve performs all unification, erroring, and substituting for the current
// type context.  This should be called only at the end of the context once all
// constraints have been built. It returns a flag indicating whether or not
// solving succeeded.  This function also clears the current solving context.
func (s *Solver) Solve() bool {
	succeeded := true

	// unify all constraints
	for _, cons := range s.Constraints {
		_, ok := s.unify(cons.Lhs, cons.Rhs, cons.Kind, cons.Position)
		succeeded = succeeded && ok
	}

	// test to see if all variables resolved
	for _, tvar := range s.Variables {
		if sub, ok := s.Substitutions[tvar.ID]; ok {
			// if the best substituted type is a type constraint, then we
			// attempt to find a default type.  If one can't be found, then this
			// type is still unsolvable (something being `Numeric` doesn't
			// really help much)
			if _, ok := sub.SubbedType.(*ConstraintType); ok {
				if tvar.DefaultType != nil {
					tvar.Unknown.EvalType = tvar.DefaultType
					continue
				}
			} else {
				tvar.Unknown.EvalType = sub.SubbedType
				continue
			}
		}

		tvar.LogUnsolvable()
		succeeded = false
	}

	// reset solver
	s.Constraints = nil
	s.Variables = make(map[int]*TypeVariable)
	s.Substitutions = make(map[int]*TypeSubstitution)

	return succeeded
}

// -----------------------------------------------------------------------------

// These values indicate positionally which type is most general of two types:
// known as the type dominance.  These values are known as type relations.  They
// are used in unification.
const (
	UEqual = iota // Equal Dominance
	ULeft         // Left Dominant
	URight        // Right Dominant
)

// unify implements type unification: it takes two types and finds a
// substitution that satisfies the constraint between then if such a
// substitution exists. `consKind` indicates the relationship between the two
// types that must be deduced to satisfy the constraint (equal, coercible, etc)
// -- it should be one of the enumerated constraint kinds.  This function does
// log errors and should not be called as a "testing" function.
func (s *Solver) unify(lhType, rhType DataType, consKind int, pos *logging.TextPosition) (int, bool) {
	// we start by testing the `rhType` to see if it is unknown before
	// proceeding with unification -- the main unify switch tests based on the
	// `lhType`.
	if rut, ok := rhType.(*UnknownType); ok {
		// we do NOT need to flip constraints here since we substituting to the
		// right which doesn't change the "constraint orientation".  Eg.,
		// consider we have the constraint `Numeric >= t1`.  It we are
		// substituting `Numeric` for `t1`, we don't need to flip the constraint
		// since any substitutions after this one must still be a subset of
		// `Numeric`.
		if sub, ok := s.Substitutions[rut.TypeVarID]; ok {
			if tr, ok := s.unify(lhType, sub.SubbedType, sub.ConsKind, pos); ok {
				// only if the left type was dominant, do we need to update the
				// substitution. `unify` already checks that the conditions of
				// this left substitution were met so we don't need to check
				// them here
				if tr == ULeft {
					sub.SubbedType = lhType

					// since we are performing a substitution against a
					// different constraint, we need to update the substitution
					// to indicate which constraint we are abiding by now (for
					// the substitution)
					sub.ConsKind = consKind
				}

				return tr, true
			} else {
				return -1, false
			}
		} else {
			s.Substitutions[rut.TypeVarID] = &TypeSubstitution{
				SubbedType: lhType,
				ConsKind:   consKind,
			}
			return UEqual, true
		}
	}

	// all types with constructors have to be tested for unification. Note that
	// most types will assert strict equality on their subtypes for unification.
	// The only types that propagate the constraint kind are unknowns
	switch v := lhType.(type) {
	case *UnknownType:
		if sub, ok := s.Substitutions[v.TypeVarID]; ok {
			// because this type is on the left, we need to flip the constraint
			// orientation for TCSubset and TCSuperset.  The understand why
			// consider the example: `t1 <= Numeric`.  Any future substitutions
			// must be subsets of `Numeric` so even the constraint says
			// "superset", we need to have the substitution constraint be a
			// subset constraint.  Casts never cause left substitution (since
			// the known type we are casting to is always on the left) so we
			// don't need to handle "flipping" them.
			switch consKind {
			case TCSubset:
				consKind = TCSuperset
			case TCSuperset:
				consKind = TCSubset
			}

			if tr, ok := s.unify(sub.SubbedType, rhType, consKind, pos); ok {
				// only if the right type was dominant, do we need to update the
				// substitution. `unify` already checks that the conditions of
				// this left substitution were met so we don't need to check
				// them here
				if tr == URight {
					sub.SubbedType = rhType
					sub.ConsKind = consKind
				}

				return tr, true
			} else {
				return -1, false
			}
		} else {
			s.Substitutions[v.TypeVarID] = &TypeSubstitution{
				SubbedType: rhType,
				ConsKind:   consKind,
			}
			return UEqual, true
		}
	case TupleType:
		if rtt, ok := rhType.(TupleType); ok {
			if len(v) == len(rtt) {
				result := true

				for i, item := range v {
					_, ok := s.unify(item, rtt[i], TCEquality, pos)
					result = result && ok
				}

				return UEqual, result
			}
		}
	case *VectorType:
		if rvt, ok := rhType.(*VectorType); ok {
			if v.Size == rvt.Size {
				return s.unify(v.ElemType, rvt.ElemType, TCEquality, pos)
			}
		}
	case *RefType:
		if rrt, ok := rhType.(*RefType); ok {
			if v.Constant == rrt.Constant {
				return s.unify(v.ElemType, rrt.ElemType, TCEquality, pos)
			}
		}
	case *FuncType:
		if rft, ok := rhType.(*FuncType); ok {
			if len(v.Args) == len(rft.Args) && v.Async == rft.Async {
				result := true

				for i, arg := range v.Args {
					if rft.Args[i].Name != arg.Name || arg.Optional != rft.Args[i].Optional || arg.Indefinite != rft.Args[i].Indefinite {
						s.logTypeMismatch(lhType, rhType, TCEquality, pos)
						return -1, false
					}

					_, ok := s.unify(arg.Val.Type, rft.Args[i].Val.Type, TCEquality, pos)
					result = result && ok
				}

				_, ok := s.unify(v.ReturnType, rft.ReturnType, TCEquality, pos)
				return UEqual, result && ok
			}
		}
	case *GenericInstanceType:
		// if the other type is not also a generic instance, then this is always
		// a type mismatch because types can only be defined as generic: it is
		// not possible for two equal types to exist as a non-generic and as a
		// generic.  Therefore, we can check subtypes trivially by just unifying
		// the lists of parameters if the two types share the same generic
		// parent.
		if rgi, ok := rhType.(*GenericInstanceType); ok {
			if v.Generic.equals(rgi.Generic) {
				result := true

				for i, vtparam := range v.TypeParams {
					_, ok := s.unify(vtparam, rgi.TypeParams[i], TCEquality, pos)
					result = result && ok
				}

				return UEqual, result
			}
		}
	default:
		// all other types, although they may contain values with subtypes, are
		// not going to require any additional unification because those
		// subtypes will always be known since defined types that are not
		// generic must specify known types in their definitions
		switch consKind {
		case TCEquality:
			if Equals(lhType, rhType) {
				return UEqual, true
			}
		case TCSubset:
			if s.CoerceTo(rhType, lhType) {
				return ULeft, true
			}
		case TCCast:
			// TODO: check to see if this is ok...
			if s.CastTo(rhType, lhType) {
				return ULeft, true
			}
		case TCSuperset:
			if s.CoerceTo(lhType, rhType) {
				return URight, true
			}
		}
	}

	// all cases that reach here are type mismatches
	s.logTypeMismatch(lhType, rhType, consKind, pos)
	return -1, false
}

// logTypeMismatch logs a type mismatch error between two types.  It takes a
// constraint kind to indicate what error it should log
func (s *Solver) logTypeMismatch(lhType, rhType DataType, consKind int, pos *logging.TextPosition) {
	var message string
	switch consKind {
	case TCEquality:
		message = fmt.Sprintf("Type Mismatch: `%s` v `%s`", lhType.Repr(), rhType.Repr())
	case TCSubset:
		message = fmt.Sprintf("Invalid Coercion: `%s` to `%s`", rhType.Repr(), lhType.Repr())
	case TCSuperset:
		message = fmt.Sprintf("Invalid Coercion: `%s` to `%s`", lhType.Repr(), rhType.Repr())
	case TCCast:
		message = fmt.Sprintf("Invalid Cast: `%s` to `%s`", rhType.Repr(), lhType.Repr())
	}

	logging.LogCompileError(
		s.Context,
		message,
		logging.LMKTyping,
		pos,
	)
}
