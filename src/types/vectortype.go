package types

import (
	"fmt"
)

// VectorType represents a Whirlwind vector type
type VectorType struct {
	ElemType DataType
	Size     uint
}

func (vt *VectorType) equals(other DataType) bool {
	if ovt, ok := other.(*VectorType); ok {
		return Equals(vt.ElemType, ovt.ElemType) && vt.Size == ovt.Size
	}

	return false
}

// vector types don't implement any form of coercion
func (vt *VectorType) coerce(other DataType) bool {
	return false
}

// vector types can support covariant casting
func (vt *VectorType) cast(other DataType) bool {
	if ovt, ok := other.(*VectorType); ok {
		return CastTo(vt.ElemType, ovt.ElemType) && vt.Size == ovt.Size
	}

	return false
}

// SizeOf a vector type is just SizeOf(etype) * vsize
func (vt *VectorType) SizeOf() uint {
	return vt.ElemType.SizeOf() * vt.Size
}

// AlignOf a vector type is its size (due to how vectors are used)
func (vt *VectorType) AlignOf() uint {
	return vt.SizeOf()
}

// Repr of a vector type is <ElemType * Size>
func (vt *VectorType) Repr() string {
	return fmt.Sprintf("<%s * %d>", vt.ElemType.Repr(), vt.Size)
}

func (vt *VectorType) copyTemplate() DataType {
	return &VectorType{
		ElemType: vt.ElemType.copyTemplate(),
		Size:     vt.Size,
	}
}
