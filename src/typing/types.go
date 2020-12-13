package typing

// The Whirlwind Type System is represented in 7 fundamental
// types from which all others derive.  These types are follows:
// 1. Primitives -- Single unit types, do not contain sub types
// 2. Tuples -- A pairing of n-types defines an n-tuple
// 3. Vectors -- A n-length, uniform type array of numeric primitives
// 4. References -- A type referencing a value through a pointer
// 5. Structures -- A record of named, typed fields
// 6. Interfaces -- A type that groups types based on shared behavior
// 7. Algebraic Types - A type that contains a finite number of enumerated values
// 8. Type Sets -- A set/union of multiple type values
// 9. Regions -- The typing of a region literal

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

// TupleType represents a tuple
type TupleType []DataType

// VectorType represents a vector
type VectorType struct {
	ElemType DataType
	Size     uint
}

// RefType represents a reference type
type RefType struct {
	ElemType        DataType
	Block, Constant bool
	Owned           bool

	// This field may remain nil until memory analysis occurs which means that
	// it is not a good indicator of whether or not a reference is free thus the
	// additional `Owned` field.
	Region *RegionType
}

// RegionType represents the typing of a region literal
type RegionType struct {
	Id, Rank int

	// This field can be nil if there is no parent
	Parent *RegionType

	// This slice may be empty if there are no children
	Children []*RegionType
}

// FuncType represents a function
type FuncType struct {
	Params         map[string]*TypeValue
	ReturnType     DataType
	Boxed, Boxable bool
	Constant       bool
}

// StructType represents a structure type
type StructType struct {
	Name     string
	Fields   map[string]*TypeValue
	Packed   bool
	Inherits []*StructType
}

// TypeValue represents a value-like component of a type
type TypeValue struct {
	Type               DataType
	Constant, Volatile bool
}

// InterfType represents an interface type
type InterfType struct {
	Methods map[string]*InterfMethod

	// Indicates whether the interface is a type interface or not
	TypeInterf bool

	// Implements can also be generic thus DataType instead of *InterfType
	Implements []DataType

	// Instances lists the various interfaces that implement/are instances of this interface
	Instances []DataType
}

// InterfMethod represents a method in an interface
type InterfMethod struct {
	// Methods can be generic so we accept any type here
	Signature DataType

	// Can be any one of the enumerated method kinds below
	Kind int
}

const (
	MKVirtual   = iota // Method that is given a body in a parent interface
	MKOveride          // Method that overrides a virtual method implementation in a derived interf
	MKAbstract         // Method that is defined without a body in a parent interface (to be defined)
	MKImplement        // Method that implements an abstract method
	MKStandard         // Method that is defined on a type interface that is not an override or abstract implementation
)

// AlgebraicType represents an algebraic type
type AlgebraicType struct {
	Instances map[string]*AlgebraicInstance
}

// AlgebraicInstance is a type that is an instance of a larger algebraic type
type AlgebraicInstance struct {
	Name   string
	Values []DataType
}

// TypeSet represents a type set
type TypeSet []DataType
