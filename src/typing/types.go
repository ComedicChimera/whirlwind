package typing

// The Whirlwind Type System is represented in 7 fundamental
// types from which all others derive.  These types are follows:
// 1. Primitives -- Single unit types, do not contain sub types
// 2. Tuples -- A pairing of n-types defines an n-tuple
// 3. Vectors -- A n-length, uniform type array of numeric primitives
// 4. References -- A type referencing a value through a pointer
// 5. Structures -- A record of named, typed fields
// 6. Type Sets -- A type which can assume the form of any type in its set
// 7. Algebraic Types - A type that contains a finite number of enumerated
// values (that can contain sub-values).
// All of these types are members of the type interface; interfaces are
// considered type sets, builtin collections are considered structures.

// DataType is the general interface for all data types
type DataType interface {
	// TODO
}

// Primitive Types
type PrimitiveType struct {
	// PrimKind is the general kind of primitive (Integral, Floating, etc.)
	PrimKind uint8

	// PrimSpec is the specific kind of primitive (uint, float, etc.)
	// Ordered from smallest value to largest (for non-integral types)
	PrimSpec uint8
}

// The general primitive type kinds
const (
	PrimKindIntegral = iota // integral types
	PrimKindFloating        // floating-point types
	PrimKindText            // runes and strings
	PrimKindUnit            // any and nothing
	PrimKindBoolean         // bool
)

// The various kinds of integral types
const (
	PrimIntByte = iota
	PrimIntSbyte
	PrimIntUshort
	PrimIntShort
	PrimIntUint
	PrimIntInt
	PrimIntUlong
	PrimIntLong
)

// VectorType represents a vector
type VectorType struct {
	ElemType DataType
	Size     uint
}

// TupleType represents a tuple
type TupleType []DataType

// FuncType represents a function
type FuncType struct {
	Params         map[string]*TypeValue
	ReturnType     DataType
	Boxed, Boxable bool
	Constant       bool
}

// StructType represents a structure type
type StructType struct {
	Name    string
	Members map[string]*TypeValue
	Packed  bool
}

// TypeValue represents a value-like component of a type
type TypeValue struct {
	Type               DataType
	Constant, Volatile bool
}
