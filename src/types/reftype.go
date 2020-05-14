package types

// ReferenceType represents the type of reference (eg. &int)
type ReferenceType struct {
	ElemType        DataType
	Owned, Constant bool
}

// NewReferenceType creates a reference type
//  and initializes it in the type table
func NewReferenceType(eType DataType, owned bool, constant bool) DataType {
	return newType(&ReferenceType{ElemType: eType, Owned: owned, Constant: constant})
}

func (rt *ReferenceType) cast(other DataType) bool {
	return false
}

func (rt *ReferenceType) coerce(other DataType) bool {
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

// PointerType represents the type of a pointer
type PointerType struct {
	ElemType DataType
	Constant bool
}

// NewPointerType creates a new pointer type
// and initializes it in the type table
func NewPointerType(eType DataType, constant bool) DataType {
	return newType(&PointerType{ElemType: eType, Constant: constant})
}

func (pt *PointerType) cast(other DataType) bool {
	return false
}

func (pt *PointerType) coerce(other DataType) bool {
	return false
}

// SizeOf a pointer type is always the size of a pointer
func (pt *PointerType) SizeOf() uint {
	return PointerSize
}

// AlignOf a pointer type is always its size
func (pt *PointerType) AlignOf() uint {
	return PointerSize
}
