package types

// TypeJudgement is a data type used to represent a dependency between two open
// types (ie. one type affects the other type).  Most notably, it represent a
// possible decision that can made about one or more types based on the
// deduction of a type for an affiliated open type.  It is used to faciliate
// downward type deduction while accounting for the effects of operator and
// function applications on the possible types something can hold.
type TypeJudgement struct {
	// ExpectedType is the type that the correspondent type in the parent
	// OpenType's type state must be coercible to so that this judgement passes.
	ExpectedType DataType

	// Conns is a list of all of the connected types for the judgement where
	// LinkedType is the connected open type and FillInType is the type that
	// should be deduced for the LinkedType if this judgement is passed.
	Conns []struct {
		LinkedType *OpenType
		FillInType DataType
	}
}

// OpenType is a type created during type deduction to represent a currently
// unknown type.  This type will accumulate usage information and will assume
// the most specific type possible but it will also generalize as necessary.
type OpenType struct {
	// TypeState is the set of types stored by the OpenType that is currently
	// being inferred.  This is a slice because multiple types can be considered
	// for any given open type is certain situations.  If this slice is empty,
	// the current type state is indeterminate.  If the state is indeterminate
	// at the end of analysis, this type is undeducible.  Similarly, if there
	// are multiple items in this field, it is unified and if such unification
	// fails, the type is also then considered indeducible.
	TypeState []DataType

	// Judgements is a list of all the possible judgements associated with the
	// deduction or alteration of the type state.  The positions of the
	// TypeJudgement structs correspond to the type in the type state that will
	// cause them to pass.  Eg. in the expression: `e1: t1 + e2: t2 -> t3`, all
	// of the possible forms of the `+` operator correspond to different
	// judgements to be made about the types `t1` and `t2` based on the type
	// determined for `t3`; eg. if `t3` is a string, then `t1` and `t2` must be
	// string-like by the standard definitions of the `+` operator.  Note that
	// in order for a type state to be valid, it must be pass its judgement.
	// Meaning that if a more general type is deduced that no longer passes the
	// judgement, that state element is invalid.  If the state is unified, then
	// any corresponding judgement that passes will allow the unified element to
	// be valid (ie. only one judgement must pass on unification)
	Judgements []TypeJudgement
}
