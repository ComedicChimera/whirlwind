package types

import (
	"strings"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// ReferenceType represents the type of reference (eg. &int)
type ReferenceType struct {
	ElemType     DataType
	Local, Owned bool
	Constant     bool
}

// reference types can cast on their elem type
func (rt *ReferenceType) cast(other DataType) bool {
	if ort, ok := other.(*ReferenceType); ok {
		return CastTo(rt.ElemType, ort.ElemType) && rt.compareRefAspects(ort)
	}

	return false
}

// a mutable reference can be coerced to a const reference
func (rt *ReferenceType) coerce(other DataType) bool {
	if ort, ok := other.(*ReferenceType); ok {
		return CoerceTo(rt.ElemType, ort.ElemType) && rt.compareRefAspects(ort)
	}

	return false
}

func (rt *ReferenceType) equals(other DataType) bool {
	if ort, ok := other.(*ReferenceType); ok {
		return Equals(rt.ElemType, ort.ElemType) && rt.compareRefAspects(ort)
	}

	return false
}

// compareRefAspects checks that all of the aspects of a reference types are
// equal with the exception of reference data types
func (rt *ReferenceType) compareRefAspects(ort *ReferenceType) bool {
	return rt.Constant == ort.Constant && rt.Local == ort.Local && rt.Owned == ort.Owned
}

// SizeOf a reference type is just the size of a pointer
func (*ReferenceType) SizeOf() uint {
	return util.PointerSize
}

// AlignOf a reference type is also the alignment of a pointer
func (*ReferenceType) AlignOf() uint {
	return util.PointerSize
}

// Repr of a reference type just rebuilds its type signature
func (rt *ReferenceType) Repr() string {
	reprValue := strings.Builder{}

	if rt.Owned {
		reprValue.WriteString("own ")
	}

	if rt.Local {
		reprValue.WriteString("local ")
	} else {
		reprValue.WriteString("nonlocal ")
	}

	reprValue.WriteRune('&')

	if rt.Constant {
		reprValue.WriteString("const ")
	}

	reprValue.WriteString(rt.ElemType.Repr())

	return reprValue.String()
}

func (rt *ReferenceType) copyTemplate() DataType {
	return &ReferenceType{
		ElemType: rt.ElemType.copyTemplate(),
		Local:    rt.Local, Owned: rt.Owned,
		Constant: rt.Constant,
	}
}
