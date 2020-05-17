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

// reference types can cast on their elem type
func (rt *ReferenceType) cast(other DataType) bool {
	if ort, ok := other.(*ReferenceType); ok {
		return CastTo(rt.ElemType, ort.ElemType) && rt.Constant == ort.Constant && rt.Owned == ort.Owned
	} else if opt, ok := other.(*PointerType); ok {
		if opt.isBytePointer() {
			return true
		}

		return CastTo(rt.ElemType, opt.ElemType) && rt.Constant == opt.Constant
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

// pointer types can cast on their elem type, be cast to a reference
// and all pointer types and reference types can be cast to a byte
// pointer to allow for c-binding (ie. as an analog to `void*`)
// finally, arrays can also be cast to pointers (although this is
// considered unsafe and should result in a warning)
func (pt *PointerType) cast(other DataType) bool {
	if opt, ok := other.(*PointerType); ok {
		if pt.isBytePointer() {
			return true
		}

		return CastTo(pt.ElemType, opt.ElemType) && pt.Constant == opt.Constant
	} else if ort, ok := other.(*ReferenceType); ok {
		if pt.isBytePointer() {
			return true
		}

		return CastTo(pt.ElemType, ort.ElemType) && pt.Constant == ort.Constant
	}

	return false
}

// simple check to see if a pointer is a byte pointer
func (pt *PointerType) isBytePointer() bool {
	if primType, ok := pt.ElemType.(PrimitiveType); ok {
		return primType == PrimByte
	}

	return false
}

// pointer types can coerce on their elem type
func (pt *PointerType) coerce(other DataType) bool {
	if opt, ok := other.(*PointerType); ok {
		return CoerceTo(pt.ElemType, opt.ElemType) && pt.Constant == opt.Constant
	}

	return false
}

func (pt *PointerType) equals(other DataType) bool {
	if opt, ok := other.(*PointerType); ok {
		return Equals(pt.ElemType, opt.ElemType) && pt.Constant == opt.Constant
	}

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

// Repr of a pointer type also just rebuilds type label
func (pt *PointerType) Repr() string {
	if pt.Constant {
		return "const* " + pt.ElemType.Repr()
	}

	return "*" + pt.ElemType.Repr()
}
