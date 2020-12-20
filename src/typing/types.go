package typing

import (
	"fmt"
	"strings"
)

// The Whirlwind Type System is represented in 9 fundamental types from which
// all others derive.  These types are follows:
// 1. Primitives -- Single unit types, do not contain sub types
// 2. Tuples -- A pairing of n-types defines an n-tuple
// 3. Vectors -- A n-length, uniform type array of numeric primitives
// 4. References -- A type referencing a value through a pointer
// 5. Structures -- A record of named, typed fields
// 6. Interfaces -- A type that groups types based on shared behavior
// 7. Algebraic Types - A type that contains a finite number of enumerated values
// 8. Type Sets -- A set/union of multiple type values
// 9. Regions -- The typing of a region literal
// There are several other types such as AlgebraicInstances and WildcardTypes
// that are not actually considered "fundamental types" but rather semantic
// constructs to assist in compilation and type analysis.

// DataType is the general interface for all data types
type DataType interface {
	// Repr returns the string representation of a type
	Repr() string

	// Equals tests if two data types are equivalent.  Unfortunately,
	// reflect.DeepEqual can't fulfill this task since certain types have
	// fields/values that do not effect their equivalency but that vary between
	// different instances of the type.
	Equals(dt DataType) bool
}

// -----------------------------------------------------

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
	PrimKindUnit            // nothing and any
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

func (pt *PrimitiveType) Equals(other DataType) bool {
	if opt, ok := other.(*PrimitiveType); ok {
		return pt.PrimKind == opt.PrimKind && pt.PrimSpec == opt.PrimSpec
	}

	return false
}

// Numeric checks if the given PrimType is considered `Numeric`
func (pt *PrimitiveType) Numeric() bool {
	return pt.PrimKind == PrimKindIntegral || pt.PrimKind == PrimKindFloating
}

// -----------------------------------------------------

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

func (tt TupleType) Equals(other DataType) bool {
	// Imagine if Go had a map function... (writing before Go generics)
	if ott, ok := other.(TupleType); ok {
		for i, item := range tt {
			if !item.Equals(ott[i]) {
				return false
			}
		}

		return true
	}

	return false
}

// -----------------------------------------------------

// VectorType represents a vector
type VectorType struct {
	ElemType DataType
	Size     uint
}

func (vt *VectorType) Repr() string {
	return fmt.Sprintf("<%d>%s", vt.Size, vt.ElemType.Repr())
}

func (vt *VectorType) Equals(other DataType) bool {
	if ovt, ok := other.(*VectorType); ok {
		return vt.ElemType.Equals(ovt.ElemType) && vt.Size == ovt.Size
	}

	return false
}

// -----------------------------------------------------

// RefType represents a reference type
type RefType struct {
	// Id is used to unique identify this reference during rank analysis. This
	// field should be exchanged across coercions and other such operations as
	// necessary.
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

func (rt *RefType) Equals(other DataType) bool {
	if ort, ok := other.(*RefType); ok {
		return (rt.ElemType.Equals(ort.ElemType) &&
			rt.Constant == ort.Constant &&
			rt.Owned == ort.Owned &&
			rt.Block == ort.Block &&
			rt.Global == ort.Global)
	}

	return false
}

// -----------------------------------------------------

// RegionType represents the typing of a region literal. It is simply an integer
// that is the region's identifier. All rank analysis occurs as part of the
// validator and is not stored here.
type RegionType int

func (rt RegionType) Repr() string {
	return "region"
}

func (rt RegionType) Equals(other DataType) bool {
	// `other is RegionType`... wouldn't that be nice?
	if _, ok := other.(RegionType); ok {
		// all region types are equivalent
		return true
	}

	return false
}

// -----------------------------------------------------

// FuncType represents a function
type FuncType struct {
	Params         []*FuncParam
	ReturnType     DataType
	Boxed, Boxable bool
	Constant       bool
	Async          bool
}

// FuncParam represents a function parameter
type FuncParam struct {
	Name                 string
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

func (ft *FuncType) Equals(other DataType) bool {
	if oft, ok := other.(*FuncType); ok {
		if len(ft.Params) != len(oft.Params) {
			return false
		}

		// Function parameters must be in the same order
		for i, param := range ft.Params {
			oparam := oft.Params[i]

			// Two parameters must either have the same name or have no name and
			// be in the correct position (as is the case for arguments in the
			// function data type)
			if param.Name == oparam.Name || param.Name == "" || oparam.Name == "" {
				// We don't care about volatility and *value* constancy when
				// comparing function signatures since both values don't
				// actually effect what can be passed in and what will be
				// produced by the function.  Also, value constancy being
				// ignored doesn't cause any actual constancy violations since
				// whatever is passed in will be copied and reference constancy
				// holds.
				if !(param.Val.Type.Equals(oparam.Val.Type) &&
					param.Indefinite == oparam.Indefinite &&
					param.Optional == oparam.Optional) {
					return false
				}
			} else {
				return false
			}

		}

		// intrinsic functions are not the same (nor should they be treated) as
		// regular functions (although I doubt this will ever come up since
		// instrinsics can't be be boxed anyway ¯\_(ツ)_/¯).  Constancy can't be
		// emulated/denoted in a function type literal and so that field doesn't
		// matter for the purposes of type equality (it is however taken into
		// account when comparing interface methods).  Whether or not a function
		// is boxed should also be irrelevant here.
		return ft.Async == oft.Async && ft.Boxable == oft.Boxable
	}

	return false
}

// -----------------------------------------------------
// Equality for all defined types is trivial since two defined types must refer
// to the same declaration if their name and package ID are the same since only
// one such type by any particular name may be declared in the same package.
// Thus, we can just compare the name and package ID to test for equality.  It
// does make one wish could had generics though.
// -----------------------------------------------------

// StructType represents a structure type
type StructType struct {
	Name         string
	SrcPackageID uint
	Fields       map[string]*TypeValue
	Packed       bool
	Inherits     []*StructType
}

func (st *StructType) Repr() string {
	return st.Name
}

func (st *StructType) Equals(other DataType) bool {
	if ost, ok := other.(*StructType); ok {
		return st.Name == ost.Name && st.SrcPackageID == ost.SrcPackageID
	}

	return false
}

// TypeValue represents a value-like component of a type
type TypeValue struct {
	Type               DataType
	Constant, Volatile bool
}

func (tv *TypeValue) Equals(otv *TypeValue) bool {
	return tv.Type.Equals(otv.Type) && tv.Constant == otv.Constant && tv.Volatile == otv.Volatile
}

// -----------------------------------------------------

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

func (it *InterfType) Equals(other DataType) bool {
	if oit, ok := other.(*InterfType); ok {
		return it.Name == oit.Name && it.SrcPackageID == oit.SrcPackageID
	}

	return false
}

// -----------------------------------------------------

// AlgebraicType represents an algebraic type
type AlgebraicType struct {
	Name         string
	SrcPackageID uint

	Instances map[string]*AlgebraicInstance
}

func (at *AlgebraicType) Repr() string {
	return at.Name
}

func (at *AlgebraicType) Equals(other DataType) bool {
	if oat, ok := other.(*AlgebraicType); ok {
		return at.Name == oat.Name && at.SrcPackageID == oat.SrcPackageID
	}

	return false
}

// AlgebraicInstance is a type that is an instance of a larger algebraic type
type AlgebraicInstance struct {
	Parent *AlgebraicType
	Name   string
	Values []DataType
}

func (ai *AlgebraicInstance) Repr() string {
	baseName := fmt.Sprintf("%s::%s", ai.Parent.Repr(), ai.Name)

	if len(ai.Values) == 0 {
		return baseName
	} else {
		sb := strings.Builder{}
		sb.WriteString(baseName)

		sb.WriteRune('(')
		for i, val := range ai.Values {
			sb.WriteString(val.Repr())

			if i < len(ai.Values)-1 {
				sb.WriteString(", ")
			}
		}
		sb.WriteRune(')')

		return sb.String()
	}
}

// Algebraic instances are equal if they have the same parent and name and
// equivalent values in the same positions (since values are initialized
// positionally).
func (ai *AlgebraicInstance) Equals(other DataType) bool {
	if oai, ok := other.(*AlgebraicInstance); ok {
		if !ai.Parent.Equals(oai.Parent) {
			return false
		}

		if len(ai.Values) != len(oai.Values) {
			return false
		}

		for i, value := range ai.Values {
			if !value.Equals(oai.Values[i]) {
				return false
			}
		}

		return ai.Name == oai.Name
	}

	return false
}

// -----------------------------------------------------

// TypeSet represents a type set
type TypeSet struct {
	Name         string
	SrcPackageID uint

	Types []DataType
}

func (ts *TypeSet) Repr() string {
	return ts.Name
}

func (ts *TypeSet) Equals(other DataType) bool {
	if ots, ok := other.(*TypeSet); ok {
		return ts.Name == ots.Name && ts.SrcPackageID == ots.SrcPackageID
	}

	return false
}
