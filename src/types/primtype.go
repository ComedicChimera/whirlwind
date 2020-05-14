package types

// Primitive Type values (primitive types store no additional information)
const (
	// integral types (order describes coercibility, coerce up)
	PrimSByte = iota
	PrimByte
	PrimShort
	PrimUShort
	PrimInt
	PrimUInt
	PrimLong
	PrimULong

	// boolean type
	PrimBool

	// floating types
	PrimFloat
	PrimDouble

	// stringlike types
	PrimChar
	PrimString
)

// PrimitiveType represents one of Whirlwind's primitive types (intended to store
// a constant of the form PrimName), should use NewPrimitive even though just a rename!
type PrimitiveType int

// NewPrimitiveType calls our internal newType method and then returns an
//  appropriate primitive of the desired kind (creates necessary type table entry)
func NewPrimitiveType(primKind int) DataType {
	return newType(PrimitiveType(primKind))
}

func (p PrimitiveType) cast(other DataType) bool {
	if po, ok := other.(PrimitiveType); ok {
		if p < PrimChar && po < PrimChar {
			// casting between all numeric, byte, and boolean types
			return true
		} else if p.SizeOf() == po.SizeOf() {
			// coercion between 32bit integrals and char type (using SizeOf workaround/shorthand)
			return true
		}
	}

	return false
}

func (p PrimitiveType) coerce(other DataType) bool {
	// check coercion between primitive types
	if po, ok := other.(PrimitiveType); ok {
		if p < PrimBool {
			// coercion between integral and byte types
			// (integral upward coercion)
			return po < p

		} else if p == PrimDouble {
			// integral or float to double
			return (p > PrimByte && p < PrimBool) || p == PrimFloat

		} else if p == PrimFloat {
			// small integral to float
			return p > PrimByte && p < PrimLong

		} else if p == PrimString && po == PrimChar {
			// char to string
			return true
		}

	}

	return false
}

// AlignOf a primitive type is simply the size of that type
func (p PrimitiveType) AlignOf() uint {
	return p.SizeOf()
}

// SizeOf a primitive type is determined statically as specified in specification
func (p PrimitiveType) SizeOf() uint {
	switch p {
	case PrimBool, PrimByte, PrimSByte:
		return 1
	case PrimShort, PrimUShort:
		return 2
	case PrimInt, PrimUInt, PrimChar, PrimFloat:
		return 4
	case PrimLong, PrimULong, PrimDouble:
		return 8
	}

	// unreachable
	return 0
}
