package types

import (
	"strings"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// ReferenceType represents the type of reference (eg. &int)
type ReferenceType struct {
	ElemType        DataType
	Owned, Constant bool
	Nullable        bool
}

// reference types can cast on their elem type
func (rt *ReferenceType) cast(other DataType) bool {
	if ort, ok := other.(*ReferenceType); ok {
		return CastTo(rt.ElemType, ort.ElemType) && rt.Constant == ort.Constant && rt.Owned == ort.Owned && rt.Nullable == ort.Nullable
	}

	return false
}

// a mutable reference can be coerced to a const reference
func (rt *ReferenceType) coerce(other DataType) bool {
	if ort, ok := other.(*ReferenceType); ok {
		return rt.Owned == ort.Owned && rt.OwnershipBlindEquals(ort)
	}

	return false
}

func (rt *ReferenceType) equals(other DataType) bool {
	if ort, ok := other.(*ReferenceType); ok {
		return rt.Owned == ort.Owned && rt.OwnershipBlindEquals(ort)
	}

	return false
}

// SizeOf a reference type is just the size of a pointer
func (*ReferenceType) SizeOf() uint {
	return util.PointerSize
}

// AlignOf a reference type is also the alignment of a pointer
func (*ReferenceType) AlignOf() uint {
	return util.PointerSize
}

// OwnershipBlindEquals performs a coercion without considering whether or not
// the references are owned or unowned.
func (rt *ReferenceType) OwnershipBlindEquals(ort *ReferenceType) bool {
	return Equals(rt.ElemType, ort.ElemType) && rt.Constant == ort.Constant && rt.Nullable == ort.Nullable
}

// Repr of a reference type just rebuilds its type signature
func (rt *ReferenceType) Repr() string {
	reprValue := strings.Builder{}

	if rt.Owned {
		reprValue.WriteString("own ")
	}

	reprValue.WriteRune('&')

	if rt.Constant {
		reprValue.WriteString("const ")
	}

	reprValue.WriteString(rt.ElemType.Repr())

	if rt.Nullable {
		reprValue.WriteRune('?')
	}

	return reprValue.String()
}

func (rt *ReferenceType) copyTemplate() DataType {
	return &ReferenceType{
		ElemType: rt.ElemType.copyTemplate(),
		Owned:    rt.Owned, Constant: rt.Constant,
	}
}
