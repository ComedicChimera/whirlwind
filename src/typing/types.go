package typing

import (
	"fmt"
	"strings"
)

// The Whirlwind Type System is represented in 9 fundamental
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
	// Repr returns the string representation of a type
	Repr() string

	// CoerceTo checks if the current type is coercible to the type passed in as
	// an argument
	CoerceTo(dt DataType) bool

	// CastTo checks if the current type can be cast to the type passed in as an
	// argument
	CastTo(dt DataType) bool
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

func (p *PrimitiveType) Repr() string {
	switch p.PrimKind {
	case PrimKindBoolean:
		return "bool"
	case PrimKindText:
		if p.PrimSpec == 0 {
			return "rune"
		} else {
			return "string"
		}
	case PrimKindUnit:
		if p.PrimSpec == 0 {
			return "nothing"
		} else {
			return "any"
		}
	case PrimKindFloating:
		if p.PrimSpec == 0 {
			return "float"
		} else {
			return "double"
		}
	case PrimKindIntegral:
		switch p.PrimSpec {
		case PrimIntByte:
			return "byte"
		case PrimIntSbyte:
			return "sbyte"
		case PrimIntShort:
			return "short"
		case PrimIntUshort:
			return "ushort"
		case PrimIntInt:
			return "int"
		case PrimIntUint:
			return "uint"
		case PrimIntLong:
			return "long"
		case PrimIntUlong:
			return "ulong"
		}
	}

	// unreachable
	return ""
}

// TupleType represents a tuple
type TupleType []DataType

func (tt TupleType) Repr() string {
	s := strings.Builder{}

	s.WriteRune('(')
	for i, dt := range tt {
		s.WriteString(dt.Repr())

		if i < len(tt)-1 {
			s.WriteString(", ")
		}
	}
	s.WriteRune(')')

	return s.String()
}

// VectorType represents a vector
type VectorType struct {
	ElemType DataType
	Size     uint
}

func (vt *VectorType) Repr() string {
	return fmt.Sprintf("<%d>%s", vt.Size, vt.ElemType.Repr())
}

// RefType represents a reference type
type RefType struct {
	// Id is used to unique identify this reference during rank analysis.
	Id int

	ElemType        DataType
	Block, Constant bool
	Owned, Global   bool

	// No rank or region information is included in the data type as such
	// analysis takes place as part of the validator to facilitate more
	// sophisticated logic.
}

func (rt *RefType) Repr() string {
	sb := strings.Builder{}

	if rt.Global {
		sb.WriteString("global ")
	}

	if rt.Block {
		sb.WriteString("[&]")
	} else if rt.Owned {
		sb.WriteString("own &")
	} else {
		sb.WriteRune('&')
	}

	if rt.Constant {
		sb.WriteString("const ")
	}

	sb.WriteString(rt.ElemType.Repr())

	return sb.String()
}

// RegionType represents the typing of a region literal. It is simply an integer
// that is the region's identifier. All rank analysis occurs as part of the
// validator and is not stored here.
type RegionType int

func (rt RegionType) Repr() string {
	return "region"
}

// FuncType represents a function
type FuncType struct {
	Params         map[string]*FuncParam
	ReturnType     DataType
	Boxed, Boxable bool
	Constant       bool
	Async          bool
}

// FuncParam represents a function parameter
type FuncParam struct {
	Val                  *TypeValue
	Optional, Indefinite bool
}

func (ft *FuncType) Repr() string {
	sb := strings.Builder{}

	if ft.Async {
		sb.WriteString("async")
	} else {
		sb.WriteString("func")
	}

	sb.WriteRune('(')
	n := 0
	for _, param := range ft.Params {
		if param.Indefinite {
			sb.WriteString("...")
		} else if param.Optional {
			sb.WriteRune('~')
		}

		sb.WriteString(param.Val.Type.Repr())

		if n < len(ft.Params)-1 {
			sb.WriteString(", ")
		}

		n++
	}
	sb.WriteString(")(")

	sb.WriteString(ft.ReturnType.Repr())
	sb.WriteRune(')')

	return sb.String()
}

// StructType represents a structure type
type StructType struct {
	Name         string
	SrcPackageID uint
	Fields       map[string]*TypeValue
	Packed       bool
	Inherits     []*StructType
}

// TypeValue represents a value-like component of a type
type TypeValue struct {
	Type               DataType
	Constant, Volatile bool
}

func (st *StructType) Repr() string {
	return st.Name
}

// InterfType represents an interface type
type InterfType struct {
	Methods map[string]*InterfMethod

	// This field will be "" if this interface is a type or bound interface
	Name         string
	SrcPackageID uint

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

func (it *InterfType) Repr() string {
	if it.Name == "" {
		return "<type-interf>"
	}

	return it.Name
}

// AlgebraicType represents an algebraic type
type AlgebraicType struct {
	Name         string
	SrcPackageID uint

	Instances map[string]*AlgebraicInstance
}

// AlgebraicInstance is a type that is an instance of a larger algebraic type
type AlgebraicInstance struct {
	Name   string
	Values []DataType
}

func (at *AlgebraicType) Repr() string {
	return at.Name
}

// TypeSet represents a type set
type TypeSet struct {
	Name         string
	SrcPackageID uint

	Types []DataType
}

func (ts *TypeSet) Repr() string {
	return ts.Name
}
