package types

import (
	"strings"

	"github.com/ComedicChimera/whirlwind/src/util"
)

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

// Pass passes the type judgement and applies its effects
func (tj *TypeJudgement) Pass() {
	for _, conn := range tj.Conns {
		// use equals to fill in the base type
		conn.LinkedType.equals(conn.FillInType)
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

// Finalize attempts to produce a single, unified type for the OpenType.  This
// should be called when semantic analysis in a given context has completed.  If
// this function succeeds, the type determined will be the final unified type
// for the OpenType.  Otherwise, the type represented by the OpenType is
// undeducible.  The unified type is stored in the type state if this succeeds.
func (ot *OpenType) Finalize() bool {
	if utype, ok := Unify(ot.TypeState...); ok {
		if len(ot.Judgements) > 0 {
			// the unification only succeeds if at least one judgement passes
			for _, tjud := range ot.Judgements {
				if CoerceTo(utype, tjud.ExpectedType) {
					tjud.Pass()
					// #whereismynobreak
					goto success
				}
			}

			// no judgements passed :(
			return false
		}

	success:
		ot.TypeState = []DataType{utype}

		// if we are finalizing, deduction has finished and judgements no longer
		// matter so we can simply discard them
		ot.Judgements = nil
	}

	return false
}

// equals first attempts to test for equality against the current type state if
// such an operation is defined.  If this fails for either reason, it then
// attempts to deduce the current type from the provided type.  If such a
// deduction succeeds, it will be applied.  Otherwise, it will fail.
func (ot *OpenType) equals(other DataType) bool {
	// begin by saving and clearing the equality context
	prevEq := pureEquality
	pureEquality = true

	// make sure the context is restored before this function exits
	defer (func() {
		pureEquality = prevEq
	})()

	switch len(ot.TypeState) {
	// if the type state is empty, then the provided type is deduced
	// automatically at there is no way such a deduction could fail (no
	// judgements, no state)
	case 0:
		// also no need to do a full application: blank slate
		ot.TypeState = append(ot.TypeState, other)
		return true
	case 1:
		// only with a single item in the type state is classical equality
		// actually sensical.  NOTE: the check here does not cause any deduction
		// changes so we don't need to check or pass judgements
		if Equals(ot.TypeState[0], other) {
			return true
		}
	default:
		// when we have multiple items in the type state, we check for equality
		// against all of them.  If one them matches, that item becomes the
		// singular type for the type state and its judgement passes (if one
		// exists).  This operation is final in some sense
		for i, dt := range ot.TypeState {
			if Equals(dt, other) {
				ot.applyDeduction(dt, i)
				return true
			}
		}
	}

	// if we reach this point, we can now attempt to reconcile the OpenType with
	// provided type either by unification or reduction
	for i, dt := range ot.TypeState {
		if utype, ok := Unify(dt, other); ok {
			if i >= len(ot.Judgements) || CoerceTo(utype, ot.Judgements[i].ExpectedType) {
				// if our check succeeds, we apply our deduction
				ot.applyDeduction(utype, i)
				return true
			}
		}
	}

	return false
}

// applyDeduction applies a given type as a deduction for this OpenType
func (ot *OpenType) applyDeduction(dt DataType, jpos int) {
	// make the equality context allow for OpenTypes to be satisfied during
	// application (caller restores outer context, no need to save here)
	pureEquality = false

	// test first if any judgements exist
	if jpos < len(ot.Judgements) {
		// if it does, pass it and make it the only judgement applied
		ot.Judgements[jpos].Pass()
		ot.Judgements = []TypeJudgement{ot.Judgements[jpos]}
	}

	// when we are applying the true equality deduction, the type
	// states is narrowed accordingly (leaving one possible type)
	ot.TypeState = []DataType{dt}
}

// coerce and cast to exist for the use case of pure equals
func (ot *OpenType) coerce(other DataType) bool {
	// indeterminate open types can coerce to anything
	if len(ot.TypeState) == 0 {
		return true
	}

	// if anything in the type state passes, then coercion succeeds
	for _, dt := range ot.TypeState {
		if CoerceTo(other, dt) {
			return true
		}
	}

	return false
}

func (ot *OpenType) cast(other DataType) bool {
	// cast already checks coercion so we don't need to do the length test again

	// same logic as for coercion
	for _, dt := range ot.TypeState {
		if CastTo(other, dt) {
			return true
		}
	}

	return false
}

// Repr of an open type depends strongly on its type state.  If there is nothing
// the type state then the repr is `undetermined`.  If there is only one type in
// the state, the repr is that of the single type in the state.  Otherwise, it
// is a string representing the set of types in the type state.
func (ot *OpenType) Repr() string {
	switch len(ot.TypeState) {
	case 0:
		return "undetermined"
	case 1:
		return ot.TypeState[0].Repr()
	default:
		sb := strings.Builder{}

		sb.WriteRune('(')
		sb.WriteString(ot.TypeState[0].Repr())

		for i := 1; i < len(ot.TypeState); i++ {
			sb.WriteString(" | ")
			sb.WriteString(ot.TypeState[i].Repr())
		}

		sb.WriteRune(')')

		return sb.String()
	}
}

// SizeOf an OpenType is the size of its type state; Note:
// if the type state is ambiguous, then this function is undefined
func (ot *OpenType) SizeOf() uint {
	if len(ot.TypeState) == 1 {
		return ot.TypeState[0].SizeOf()
	}

	util.LogMod.LogFatal("Size of an indeterminate type is undefined")
	return 0
}

// AlignOf an OpenType is the alignment of its type state; Note:
// if the type state is ambiguous, then this function is undefined
func (ot *OpenType) AlignOf() uint {
	if len(ot.TypeState) == 1 {
		return ot.TypeState[0].AlignOf()
	}

	util.LogMod.LogFatal("Align of an indeterminate type is undefined")
	return 0
}

func (ot *OpenType) copyTemplate() DataType {
	// TODO
	return nil
}
