package types

// TypeJudgement is a data type used to represent a dependency between two open
// types (ie. one type affects the other type).  Most notably, it represent a
// possible decision that can made about one or more types based on the
// deduction of a type for an affiliated open type.  It is used to faciliate
// downward type deduction while accounting for the effects of operator and
// function applications on the possible types something can hold.
type TypeJudgement struct {
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
	// the current type state is determined to be `any`.
	TypeState []DataType

	// Judgements is a list of all the possible judgements associated with the
	// deduction or alteration of the type state.  The positions of the
	// TypeJudgement structs correspond to the type in the type state that will
	// cause them to pass.  Eg. in the expression: `e1: t1 + e2: t2 -> t3`, all
	// of the possible forms of the `+` operator correspond to different
	// judgements to be made about the types `t1` and `t2` based on the type
	// determined for `t3`; eg. if `t3` is a string, then `t1` and `t2` must be
	// string-like by the standard definitions of the `+` operator.
	Judgements []TypeJudgement
}
