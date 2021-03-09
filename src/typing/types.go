package typing

import (
	"fmt"
	"strings"

	"github.com/ComedicChimera/whirlwind/src/logging"
)

// The Whirlwind Type System is represented in 8 fundamental types from which
// all others derive.  These types are follows:
// 1. Primitives -- Single unit types, do not contain sub types
// 2. Tuples -- A pairing of n-types defines an n-tuple
// 3. Vectors -- A n-length, uniform type array of numeric primitives
// 4. References -- A type referencing a value through a pointer
// 5. Structures -- A record of named, typed fields
// 6. Interfaces -- A type that groups types based on shared behavior
// 7. Algebraic Types - A type that contains a finite number of enumerated values
// 8. Type Sets -- A set/union of multiple type values
// 9. Regions -- A region literal (ref to region)
// There are several other types such as AlgebraicVariants and WildcardTypes
// that are not actually considered "fundamental types" but rather semantic
// constructs to assist in compilation and type analysis.

// DataType is the general interface for all data types
type DataType interface {
	// Repr returns the string representation of a type
	Repr() string

	// equals tests if two data types are equivalent.  Unfortunately,
	// reflect.DeepEqual can't fulfill this task since certain types have
	// fields/values that do not effect their equivalency but that vary between
	// different instances of the type.  It does not handle inner types -- it
	// checks for strict/literal equality and should not be used as the primary
	// equality method
	equals(dt DataType) bool

	// copyTemplate is used to create a generic instances by duplicate a type
	// template so as to perserve the temporary values of various wildcard types
	// in the new instance without requiring that the original template retain
	// those values.  This is NOT a true deep copy -- several types that can not
	// contain WildcardTypes are not copied
	copyTemplate() DataType
}

// ContainsType checks if a slice of data types contains a type equivalent to
// the given data type (via. the equals method)
func ContainsType(dt DataType, slice []DataType) bool {
	for _, item := range slice {
		if Equals(item, dt) {
			return true
		}
	}

	return false
}

// copyTemplateSlice applies copyTemplate to a slice of data types
func copyTemplateSlice(dtSlice []DataType) []DataType {
	newList := make([]DataType, len(dtSlice))

	for i, item := range dtSlice {
		newList[i] = item.copyTemplate()
	}

	return newList
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

func (pt *PrimitiveType) equals(other DataType) bool {
	if opt, ok := other.(*PrimitiveType); ok {
		return pt.PrimKind == opt.PrimKind && pt.PrimSpec == opt.PrimSpec
	}

	return false
}

// Primitives can't store WildcardTypes so no copy is necessary
func (pt *PrimitiveType) copyTemplate() DataType {
	return pt
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

func (tt TupleType) equals(other DataType) bool {
	// Imagine if Go had a map function... (writing before Go generics)
	if ott, ok := other.(TupleType); ok {
		for i, item := range tt {
			if !Equals(item, ott[i]) {
				return false
			}
		}

		return true
	}

	return false
}

func (tt TupleType) copyTemplate() DataType {
	return TupleType(copyTemplateSlice(tt))
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

func (vt *VectorType) equals(other DataType) bool {
	if ovt, ok := other.(*VectorType); ok {
		return Equals(vt.ElemType, ovt.ElemType) && vt.Size == ovt.Size
	}

	return false
}

func (vt *VectorType) copyTemplate() DataType {
	return &VectorType{
		ElemType: vt.ElemType.copyTemplate(),
		Size:     vt.Size,
	}
}

// -----------------------------------------------------

// RefType represents a reference type
type RefType struct {
	ElemType             DataType
	Constant             bool
	Owned, Block, Global bool

	// TODO: lifetimes?
}

func (rt *RefType) Repr() string {
	sb := strings.Builder{}

	if rt.Global {
		sb.WriteString("global ")
	}

	if rt.Block {
		sb.WriteString("[&] ")
	} else if rt.Owned {
		sb.WriteString("own& ")
	} else {
		sb.WriteRune('&')
	}

	if rt.Constant {
		sb.WriteString("const ")
	}

	sb.WriteString(rt.ElemType.Repr())

	return sb.String()
}

func (rt *RefType) equals(other DataType) bool {
	if ort, ok := other.(*RefType); ok {
		return (Equals(rt.ElemType, ort.ElemType) &&
			rt.Constant == ort.Constant &&
			rt.Owned == rt.Owned &&
			rt.Global == rt.Global &&
			rt.Block == rt.Block)
	}

	return false
}

func (rt *RefType) copyTemplate() DataType {
	// one of those situations where spread initialization would be nice...
	return &RefType{
		Constant: rt.Constant,
		ElemType: rt.ElemType.copyTemplate(),
		Global:   rt.Global,
		Owned:    rt.Owned,
		Block:    rt.Block,
	}
}

// -----------------------------------------------------

// FuncType represents a function
type FuncType struct {
	Args           []*FuncArg
	ReturnType     DataType
	Boxed, Boxable bool
	Async          bool
}

// FuncArg represents a function parameter
type FuncArg struct {
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
	for _, param := range ft.Args {
		if param.Indefinite {
			sb.WriteString("...")
		} else if param.Optional {
			sb.WriteRune('~')
		}

		sb.WriteString(param.Val.Type.Repr())

		if n < len(ft.Args)-1 {
			sb.WriteString(", ")
		}

		n++
	}
	sb.WriteString(")(")

	sb.WriteString(ft.ReturnType.Repr())
	sb.WriteRune(')')

	return sb.String()
}

func (ft *FuncType) equals(other DataType) bool {
	if oft, ok := other.(*FuncType); ok {
		if len(ft.Args) != len(oft.Args) {
			return false
		}

		// Function parameters must be in the same order
		for i, arg := range ft.Args {
			oarg := oft.Args[i]

			// Two parameters must either have the same name or have no name and
			// be in the correct position (as is the case for arguments in the
			// function data type)
			if arg.Name == oarg.Name || arg.Name == "" || oarg.Name == "" {
				// We don't care about volatility and *value* constancy when
				// comparing function signatures since both values don't
				// actually effect what can be passed in and what will be
				// produced by the function.  Also, value constancy being
				// ignored doesn't cause any actual constancy violations since
				// whatever is passed in will be copied and reference constancy
				// holds.
				if !(Equals(arg.Val.Type, oarg.Val.Type) &&
					arg.Indefinite == oarg.Indefinite &&
					arg.Optional == oarg.Optional) {
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

func (ft *FuncType) copyTemplate() DataType {
	newArgs := make([]*FuncArg, len(ft.Args))

	for i, arg := range ft.Args {
		newArgs[i] = &FuncArg{
			Val:        arg.Val.copyTemplate(),
			Name:       arg.Name,
			Indefinite: arg.Indefinite,
			Optional:   arg.Optional,
		}
	}

	return &FuncType{
		Args:       newArgs,
		ReturnType: ft.ReturnType.copyTemplate(),
		Async:      ft.Async,
		Boxable:    ft.Boxable,
		Boxed:      ft.Boxed,
	}
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
	Inherit      *StructType
}

func (st *StructType) Repr() string {
	return st.Name
}

func (st *StructType) equals(other DataType) bool {
	if ost, ok := other.(*StructType); ok {
		return st.Name == ost.Name && st.SrcPackageID == ost.SrcPackageID
	}

	return false
}

func (st *StructType) copyTemplate() DataType {
	newFields := make(map[string]*TypeValue)

	for name, field := range st.Fields {
		newFields[name] = field.copyTemplate()
	}

	var newInherit *StructType
	if st.Inherit != nil {
		newInherit = st.Inherit.copyTemplate().(*StructType)
	}

	return &StructType{
		Name:         st.Name,
		SrcPackageID: st.SrcPackageID,
		Fields:       newFields,
		Packed:       st.Packed,
		Inherit:      newInherit,
	}
}

// TypeValue represents a value-like component of a type
type TypeValue struct {
	Type               DataType
	Constant, Volatile bool
}

func (tv *TypeValue) Equals(otv *TypeValue) bool {
	return Equals(tv.Type, otv.Type) && tv.Constant == otv.Constant && tv.Volatile == otv.Volatile
}

func (tv *TypeValue) copyTemplate() *TypeValue {
	return &TypeValue{
		Type:     tv.Type.copyTemplate(),
		Constant: tv.Constant,
		Volatile: tv.Volatile,
	}
}

// -----------------------------------------------------

// InterfType represents an interface type
type InterfType struct {
	Methods map[string]*InterfMethod

	// This field will be "" if this interface is a type or bound interface
	Name         string
	SrcPackageID uint

	// Implements stores only the various interfaces that this InterfType
	// implements explicitly (to prevent virtual methods from appearing on
	// interfaces that don't actually fully implement an interface)
	Implements []*InterfType

	// Instances stores the various data types that are implicit or explicit
	// instances of the InterfType. This prevents virtual methods from being
	// passed down while enabling efficient type checking by storing instances
	// that have already been matched once (memoization)
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
	MKOverride         // Method that overrides a virtual method implementation in a derived interf
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

func (it *InterfType) equals(other DataType) bool {
	if oit, ok := other.(*InterfType); ok {
		return it.Name == oit.Name && it.SrcPackageID == oit.SrcPackageID
	}

	return false
}

func (it *InterfType) copyTemplate() DataType {
	newMethods := make(map[string]*InterfMethod, len(it.Methods))

	for name, method := range it.Methods {
		newMethods[name] = &InterfMethod{
			Signature: method.Signature.copyTemplate(),
			Kind:      method.Kind,
		}
	}

	// really wishing for generics rn...
	newImplements := make([]*InterfType, len(it.Implements))
	for i, implement := range it.Implements {
		newImplements[i] = implement.copyTemplate().(*InterfType)
	}

	return &InterfType{
		Name:         it.Name,
		SrcPackageID: it.SrcPackageID,
		Methods:      newMethods,
		Implements:   newImplements,
		Instances:    copyTemplateSlice(it.Instances),
	}
}

// -----------------------------------------------------

// AlgebraicType represents an algebraic type
type AlgebraicType struct {
	Name         string
	SrcPackageID uint

	Variants []*AlgebraicVariant

	// Closed indicates whether or not this type is closed or open
	Closed bool
}

func (at *AlgebraicType) Repr() string {
	return at.Name
}

func (at *AlgebraicType) equals(other DataType) bool {
	if oat, ok := other.(*AlgebraicType); ok {
		return at.Name == oat.Name && at.SrcPackageID == oat.SrcPackageID
	}

	return false
}

func (at *AlgebraicType) copyTemplate() DataType {
	newAt := &AlgebraicType{
		Name:         at.Name,
		SrcPackageID: at.SrcPackageID,
		Closed:       at.Closed,
	}

	newAt.Variants = make([]*AlgebraicVariant, len(at.Variants))
	for i, vari := range at.Variants {
		newAt.Variants[i] = &AlgebraicVariant{
			Name:   vari.Name,
			Values: copyTemplateSlice(vari.Values),
			Parent: newAt,
		}
	}

	return newAt
}

// AlgebraicVariant is a type that denotes a specific variant of a larger
// algebraic type for use as a value (eg. so we can store it in symbols)
type AlgebraicVariant struct {
	// Parent is always an algebraic type even in the context of generics since
	// AlgebraicVariants aren't used as an actual "type" in the language -- they
	// are replaced with their generic instance parent as soon as they are
	// created
	Parent DataType

	Name   string
	Values []DataType
}

func (av *AlgebraicVariant) Repr() string {
	baseName := fmt.Sprintf("%s::%s", av.Parent.Repr(), av.Name)

	if len(av.Values) == 0 {
		return baseName
	} else {
		sb := strings.Builder{}
		sb.WriteString(baseName)

		sb.WriteRune('(')
		for i, val := range av.Values {
			sb.WriteString(val.Repr())

			if i < len(av.Values)-1 {
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
func (av *AlgebraicVariant) equals(other DataType) bool {
	if oav, ok := other.(*AlgebraicVariant); ok {
		if !Equals(av.Parent, oav.Parent) {
			return false
		}

		if len(av.Values) != len(oav.Values) {
			return false
		}

		for i, value := range av.Values {
			if !Equals(value, oav.Values[i]) {
				return false
			}
		}

		return av.Name == oav.Name
	}

	return false
}

// AlgebraicVariants should NEVER be directly in templates (because they need
// to maintain a consistent parent and should never appear in a generic
// template)
func (av *AlgebraicVariant) copyTemplate() DataType {
	logging.LogFatal("`copyTemplate` called on `AlgebraicVariant`")
	return nil
}

// -----------------------------------------------------

// TypeSet represents a type set
type TypeSet struct {
	Name         string
	SrcPackageID uint

	Types     []DataType
	Intrinsic bool
}

func (ts *TypeSet) Repr() string {
	return ts.Name
}

func (ts *TypeSet) equals(other DataType) bool {
	if ots, ok := other.(*TypeSet); ok {
		return ts.Name == ots.Name && ts.SrcPackageID == ots.SrcPackageID
	}

	return false
}

func (ts *TypeSet) copyTemplate() DataType {
	return &TypeSet{
		Name:         ts.Name,
		SrcPackageID: ts.SrcPackageID,
		Types:        copyTemplateSlice(ts.Types),
		Intrinsic:    ts.Intrinsic,
	}
}

// -----------------------------------------------------

// RegionType represents a region literal.  Its value is that region's unique ID
// -- used for memory checking.
type RegionType uint

func (rt RegionType) Repr() string {
	return "region"
}

func (rt RegionType) equals(other DataType) bool {
	// all region types are equivalent for the purposes of type checking
	_, ok := other.(RegionType)
	return ok
}

func (rt RegionType) copyTemplate() DataType {
	// regions contain no wildcard types so copyTemplate is just an identity
	return rt
}
