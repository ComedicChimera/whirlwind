package types

// ReferenceType represents the type of reference (eg. &int)
type ReferenceType struct {
	ElemType        DataType
	Owned, Constant bool
}

// NewReferenceType creates a reference type and initializes it in the type table
func NewReferenceType(eType DataType, owned bool, constant bool) DataType {
	return newType(&ReferenceType{ElemType: eType, Owned: owned, Constant: constant})
}

// reference types can cast on their elem type
func (rt *ReferenceType) cast(other DataType) bool {
	if ort, ok := other.(*ReferenceType); ok {
		return CastTo(rt.ElemType, ort.ElemType) && rt.Constant == ort.Constant && rt.Owned == ort.Owned
	}

	return false
}

// reference types can coerce on their elem type
func (rt *ReferenceType) coerce(other DataType) bool {
	if ort, ok := other.(*ReferenceType); ok {
		return CoerceTo(rt.ElemType, ort.ElemType) && rt.Constant == ort.Constant && rt.Owned == ort.Owned
	}

	return false
}

func (rt *ReferenceType) equals(other DataType) bool {
	if ort, ok := other.(*ReferenceType); ok {
		return Equals(rt.ElemType, ort.ElemType) && rt.Constant == ort.Constant && rt.Owned == ort.Owned
	}

	return false
}

// SizeOf a reference type is just the size of a pointer
func (*ReferenceType) SizeOf() uint {
	return PointerSize
}

// AlignOf a reference type is also the alignment of a pointer
func (*ReferenceType) AlignOf() uint {
	return PointerSize
}

// Borrow returns an unowned "copy" of a ReferenceType
func (rt *ReferenceType) Borrow() DataType {
	return NewReferenceType(rt.ElemType, false, rt.Constant)
}

// Repr of a reference type just rebuilds its type signature
func (rt *ReferenceType) Repr() string {
	prefix := ""

	if rt.Constant {
		prefix += "const"
	}

	if rt.Owned {
		prefix += "own"
	}

	return prefix + "& " + rt.ElemType.Repr()
}

func (rt *ReferenceType) copyTemplate() DataType {
	return NewReferenceType(rt.ElemType.copyTemplate(), rt.Owned, rt.Constant)
}
