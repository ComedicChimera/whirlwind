package types

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// VectorType represents a Whirlwind vector type
type VectorType struct {
	ElemType DataType
	Size     uint
}

// NewVectorType creates a new vector type
func NewVectorType(etype DataType, size uint) DataType {
	return &VectorType{ElemType: etype, Size: size}
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

// VectorSize is a dummy type used to implement a vector size parameter
type VectorSize int

// given an implementation for convenience in other locations
func (vs VectorSize) equals(other DataType) bool {
	if ovs, ok := other.(VectorSize); ok {
		return vs == ovs
	}

	return false
}

// all of the other methods of vector size essentially do nothing
func (vs VectorSize) coerce(other DataType) bool {
	return false
}

func (vs VectorSize) cast(other DataType) bool {
	return false
}

// AlignOf is undefined (nonsensical) for vector sizes
func (vs VectorSize) AlignOf() uint {
	util.LogMod.LogFatal("Vector sizes have no defined alignment")
	return 0
}

// SizeOf is also undefined for vector sizes
func (vs VectorSize) SizeOf() uint {
	util.LogMod.LogFatal("Vector sizes have no defined size")
	return 0
}

// Repr is only defined for debugging purposes
func (vs VectorSize) Repr() string {
	return fmt.Sprintf("vsize: %d", vs)
}

// just an identity method really
func (vs VectorSize) copyTemplate() DataType {
	return vs
}
