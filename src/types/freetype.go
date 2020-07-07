package types

// OperatorJudgement represents a superposition of possible operator forms that
// must be valid for a given free type.  To understand this idea, consider we
// have a variable with an unknown type `x` and another such variable `y`.  Now,
// let us say the compiler encounters an expression of the following form `x +
// y`.  Given that neither variable has a known type, such an expression cannot
// tell us definitively the type of either variable: there are many different
// forms of the operator `+` and only one will ultimately be valid.  However, an
// valid type for the two variables must allow them to be used in the form `x +
// y`.  This constraint is expressed in the form of an operator judgement
// applied to each variable which designates in what position the variable
// occurs in the operator application and what possible forms could be valid for
// a variable in that position.  Moreover, given that such judgements often
// produce new types and are often linked to judgements applied to other free
// types, all judgements will contain references to related judgements so that
// when a type is ultimately inferred for the whole expression, it will
// propagate properly.  NOTE: operator judgements are only used when multiple
// operator forms are deemed to be possible.
type OperatorJudgement struct {
	// Signatures stores the operator ("function") signatures of all the
	// operator forms expressed/being imposed as contraints by this judgement
	Signatures []*FuncType

	// SigPos is the position where the type will be located in the forms NOTE:
	// if this value is equals to the number of arguments to the operator then
	// it refers to the return type (product type) of the operator signature
	// (this is not a problem b/c operators cannot take optional or variadic
	// arguments - it doesn't logically make sense).
	SigPos int

	// Relations stores all of the free types related to this operator judgement
	// (ie. all of the other terms in the signature - inc. the product type).
	// NOTE: Relations[SigPos] = nil
	Relations []*FreeType
}

// Satisfy takes in a prospective type for the free type this operator judgement
// corresponds to and attempts to satisfy all the terms of judgement with it
// NOTE: should only be called if the type is already determined to be valid for
// all other aspects of the free type to which the judgement is applied
func (op *OperatorJudgement) Satisfy(dt DataType) (int, bool) {
	var matchedSig *FuncType
	sigNum := 0

	for i, sig := range op.Signatures {
		if op.SigPos == len(sig.Params) {
			if !CoerceTo(dt, sig.ReturnType) {
				continue
			}
		} else if !CoerceTo(dt, sig.Params[op.SigPos].Type) {
			continue
		}

		matchedSig = sig
		sigNum = i
		break
	}

	if matchedSig == nil {
		return -1, false
	}

	// attempt to apply this type to all related free types
	for i, rel := range op.Relations {
		if rel != nil {
			var sigDt DataType
			if i == len(matchedSig.Params) {
				sigDt = matchedSig.ReturnType
			} else {
				sigDt = matchedSig.Params[i].Type
			}

			if _, ok := rel.CanDeduce(sigDt); !ok {
				return -1, false
			}
		}
	}

	return sigNum, true
}

// FreeType is a tool used to faciliate downward type deduction. Effectively,
// when the compiler can't infer a type via upward type deduction (moving up the
// tree), it temporarily fills in the type with a free type and allows this type
// to accumulate operator judgements and interfaces that are expected for the
// given type until it eventually determines the actual type of the free type.
// Free types will attempt to fill themselves with the most specific possible
// version of the type (ie. it will prefer `int` to Integral).  Some builtin
// types also have a default type they will infer to if no other type can be
// deduced (eg. Integral has a default/most-specific type of `int`).
type FreeType struct {
	// InferredType is the current type the FreeType is holding.  This type
	// defaults to `any` given that `any` is the superset of all types
	InferredType DataType

	// CanDefault is a field set by the inferencer when it creates a free type
	// to indicate whether or not the compiler should apply any defaulting logic
	// (if such logic exists) to initial inferred type (eg. the field is set to
	// true when inferring the type of an integral literal - starts with
	// Integral as its type but ultimately the compiler will want to default to
	// more sensible default value: `int`)
	CanDefault bool

	// Judgements is used to store the operator judgements applied to the free
	// type.  See the definition of OperatorJudgements above for an explanation
	// of what an operator judgement is
	Judgements []*OperatorJudgement
}

// NewFreeType creates a new free type in the FTT for the given visitor.  If a
// starting type is provided (ie. not nil), the function assumes that the starting
// type is defaultable.  If not, the assumed inferred type is `any`.
func NewFreeType(ftt *[]*FreeType, startingType DataType) DataType {
	var ft *FreeType
	if startingType == nil {
		ft = &FreeType{InferredType: PrimitiveType(PrimAny)}
	} else {
		ft = &FreeType{InferredType: startingType, CanDefault: true}
	}

	*ftt = append(*ftt, ft)
	return ft
}

// AddJudgement adds an operator judgement to the given free type
func (ft *FreeType) AddJudgement(op *OperatorJudgement) {
	ft.Judgements = append(ft.Judgements, op)
}

// CanDeduce determines if a type is valid for the free type and returns the
// valid operator signatures/forms it used to confirm the deduction
func (ft *FreeType) CanDeduce(dt DataType) ([]int, bool) {
	// force a pure context for equality
	prevPureEq := pureEquality
	pureEquality = true

	if !CoerceTo(dt, ft.InferredType) {
		return nil, false
	}

	sigNs := make([]int, len(ft.Judgements))
	for i, jud := range ft.Judgements {
		if sigN, ok := jud.Satisfy(dt); ok {
			sigNs[i] = sigN
		} else {
			return nil, false
		}
	}

	// restore old equality context
	pureEquality = prevPureEq

	return sigNs, true
}

// applyDeduction performs a type deduction for the given free type
func (ft *FreeType) applyDeduction(dt DataType, sigs []int) {
	// TODO
}

// equals tests if the given type is equal to the inferred type first.  If not,
// it tests if the given type could be deduced for the free type and deduces it
// if so.  Otherwise, it returns false and does nothing.
func (ft *FreeType) equals(dt DataType) bool {
	if Equals(ft.InferredType, dt) {
		return true
	}

	if sigs, ok := ft.CanDeduce(dt); ok {
		ft.applyDeduction(dt, sigs)
		return true
	}

	return false
}

// coerce and cast are both used as pure variants of their respective functions
func (ft *FreeType) coerce(dt DataType) bool {
	return CoerceTo(dt, ft.InferredType)
}

func (ft *FreeType) cast(dt DataType) bool {
	return CastTo(dt, ft.InferredType)
}

// Repr of a free type is the repr of its inferred type
func (ft *FreeType) Repr() string {
	return ft.InferredType.Repr()
}

// SizeOf a free type is the size of its inferred type
func (ft *FreeType) SizeOf() uint {
	return ft.InferredType.SizeOf()
}

// AlignOf a free type is the align of its inferred type
func (ft *FreeType) AlignOf() uint {
	return ft.InferredType.AlignOf()
}

// copyTemplate copies everything to prevent logical errors
func (ft *FreeType) copyTemplate() DataType {
	copyFt := &FreeType{
		InferredType: ft.InferredType.copyTemplate(),
		CanDefault:   ft.CanDefault,
		Judgements:   make([]*OperatorJudgement, len(ft.Judgements)),
	}

	for i, jud := range ft.Judgements {
		copyJud := &OperatorJudgement{
			Signatures: make([]*FuncType, len(jud.Signatures)),
			SigPos:     jud.SigPos,
			Relations:  make([]*FreeType, len(jud.Relations)),
		}

		for j, sig := range jud.Signatures {
			copyJud.Signatures[j] = sig.copyTemplate().(*FuncType)
		}

		for j, rel := range jud.Relations {
			copyJud.Relations[j] = rel.copyTemplate().(*FreeType)
		}

		copyFt.Judgements[i] = copyJud
	}

	return copyFt
}
