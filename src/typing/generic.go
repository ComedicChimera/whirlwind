package typing

import "strings"

// GenericType represents a generic definition.  Semantically, generics are not
// really considered types; however, it is implemented as one since many of our
// other data types may need to contain generics as well as other data types
// together (eg. interface methods).
type GenericType struct {
	// TypeParams is a list of shared references to WildcardTypes that stand in
	// for each of the type parameters of this generic.  A reference may appear
	// several times in the generic template if its type parameter is used
	// multiple times in the template.  All WildcardTypes correspondent to this
	// type parameter must share the same reference.
	TypeParams []*WildcardType

	// Template is the data type that is used for generating the various
	// instances of this generic type.  It contains empty WildcardTypes that are
	// temporarily filled in and then copied.
	Template DataType
}

func (gt *GenericType) Repr() string {
	sb := strings.Builder{}
	sb.WriteString(gt.Template.Repr())

	sb.WriteRune('<')
	for i, tp := range gt.TypeParams {
		sb.WriteString(tp.Name)

		if i < len(gt.TypeParams)-1 {
			sb.WriteString(", ")
		}
	}

	sb.WriteRune('>')

	return sb.String()
}

// Equality for generics is only defined for the sake of comparing definitions
// inside data types (eg. methods) and thus it is only necessary to consider to
// generic types as having the potential to be equal; viz. we are not comparing
// generic to generic instances here.
func (gt *GenericType) Equals(other DataType) bool {
	if ogt, ok := other.(*GenericType); ok {
		if !gt.Template.Equals(ogt.Template) {
			return false
		}

		if len(gt.TypeParams) != len(ogt.TypeParams) {
			return false
		}

		for i, tp := range gt.TypeParams {
			if !tp.Equals(ogt.TypeParams[i]) {
				return false
			}
		}

		return true
	}

	return false
}

// CreateInstance is used to create a new generic instance based on the given type
// parameters.  It returns false as the second return if either the number of type
// values provided doesn't match up with the number of type parameters or some of
// the type values don't satisfy the restrictors on their corresponding parameters.
func (gt *GenericType) CreateInstance(typeValues []DataType) (DataType, bool) {
	return nil, false
}

// WildcardType is a psuedo-type that is used as a stand-in for type parameters
// and as a part of the matching mechanism for interface binding.
type WildcardType struct {
	// Name stores the type parameter name this type is standing in for (eg.
	// `T`). It is primarily used for displaying out types (ie. implementing the
	// Repr()).
	Name string

	// Restrictors stores the list of types that this WildcardType can be
	// (correspondent to the restrictors on a type parameter).  This field will
	// be `nil` if no restrictors are applied.
	Restrictors []DataType

	// Value stores the type that has been filled in for the current type.  When
	// this field is not nil, the WildcardType is considered equivalent to this type
	// (and thus all coercions and casts will now be applied to the value).
	Value DataType

	// ImmediateBind is used to denote whether or not this type should assume the
	// value of the first type it is compared to.  This is used to facilitate efficient
	// generic binding.  For regular usage in the context of generics, this field
	// should be `false`.
	ImmediateBind bool
}

func (wt *WildcardType) Repr() string {
	if wt.Value == nil {
		return wt.Name
	} else {
		return wt.Value.Repr()
	}
}

func (wt *WildcardType) Equals(other DataType) bool {
	if wt.Value == nil {
		if len(wt.Restrictors) > 0 {
			noMatches := true

			for _, r := range wt.Restrictors {
				if r.Equals(other) {
					noMatches = false
					break
				}
			}

			if noMatches {
				return false
			}
		}

		if wt.ImmediateBind {
			wt.Value = other
		}

		return true
	}

	return wt.Value.Equals(other)
}
