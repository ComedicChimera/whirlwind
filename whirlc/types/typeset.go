package types

import (
	"reflect"
	"strings"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// various kinds of a type sets that can be produced
const (
	TSKindInterf = iota
	TSKindAlgebraic
	TSKindEnum
	TSKindUnion
)

// TypeSet represents a polymorphic type that can be any of the types included
// in itself (eg. algebraic typesets, interfaces, etc.)
type TypeSet struct {
	interf *TypeInterf

	Name    string
	SetKind int
	Members []DataType
}

// NewTypeSet creates a new standard type set
func NewTypeSet(name string, sk int, members []DataType) *TypeSet {
	return &TypeSet{Name: name, SetKind: sk, Members: members, interf: nil}
}

// NewInterface creates a new type set for a provided interface (creates an
// interface type)
func NewInterface(name string, interf *TypeInterf) DataType {
	return &TypeSet{Name: name, SetKind: TSKindInterf, interf: interf}
}

// InstOf checks whether or not a data type is an element of the type set. if it
// is not explicitly an element of the type set or an element by proxy (ie.
// coerced into the type set), then it is not an instance of the type set
// regardless of quantifiers
func InstOf(elem DataType, set *TypeSet) bool {
	for _, dt := range set.Members {
		if dt == elem {
			return true
		}
	}

	return false
}

// NewEnumMember creates a new member in a given type set (enum or algebraic)
// Returns a boolean indicating whether or not the member was added successfully
func (ts *TypeSet) NewEnumMember(name string, values []DataType) bool {
	for _, member := range ts.Members {
		em := member.(*EnumMember)

		if em.Name == name {
			return false
		}
	}

	ts.Members = append(ts.Members, &EnumMember{Name: name, Values: values, Parent: ts})
	return true
}

// NewTypeSetMember adds a new type to a type set
func (ts *TypeSet) NewTypeSetMember(dt DataType) {
	for _, member := range ts.Members {
		if reflect.DeepEqual(member, dt) {
			return
		}
	}

	ts.Members = append(ts.Members, dt)
}

// coecion on type sets follows three simple rules: - if the other is an element
// of the type set, then it can be coerced to the type set. - if the type set
// has an interface qualifier and it matches the input type, then type can be
// coerced to the type set and will from then on be considered a member of the
// typeset (impl for interf duck typing) - if the type set does not have an
// interface qualifier and the type is not already a member of the type set then
// it cannot be coerced to the type set
func (ts *TypeSet) coerce(other DataType) bool {
	if InstOf(other, ts) {
		return true
	} else if ts.interf != nil && ts.interf.MatchType(other) == nil {
		ts.Members = append(ts.Members, other)
		return true
	}

	return false
}

// casting follows the same rules as coercion
func (ts *TypeSet) cast(other DataType) bool {
	return ts.coerce(other)
}

// equality compares both on name and members. other data is implicitly compared
// by name as is members, but we need to satisfy an free types that may exist
// within the type set if possible
func (ts *TypeSet) equals(other DataType) bool {
	if ots, ok := other.(*TypeSet); ok {
		if ts.Name != ots.Name {
			return false
		}

		// if we have reached this point, we know the members are the same so we
		// simply satisfy any free types that could exist here (necessary evil)
		return TypeListEquals(ts.Members, ots.Members)
	}

	return false
}

// SizeOf a type set depends on its SetKind Four Possible Sizes for a type set:
// Algebraic: sizeof(type{*i8, i16, i32}) Enum: sizeof(i16) Interf:
// sizeof(type{*i8, *vtable, i16, i32}) Union: sizeof(largest type) - mainly
// used for aliases
func (ts *TypeSet) SizeOf() uint {
	switch ts.SetKind {
	case TSKindAlgebraic:
		return util.PointerSize * 3
	case TSKindEnum:
		return 2
	case TSKindInterf:
		return util.PointerSize * 4
	default:
		// TSKindUnion
		return MaxSize(ts.Members)
	}
}

// AlignOf a type set follows similar semantics to SizeOf (see comment above for
// an enumeration of the underlying data structures by kind)
func (ts *TypeSet) AlignOf() uint {
	switch ts.SetKind {
	case TSKindAlgebraic:
		return util.PointerSize
	case TSKindEnum:
		return 2
	case TSKindInterf:
		return util.PointerSize
	default:
		// TSKindUnion
		return MaxAlign(ts.Members)
	}
}

// Repr of a type set is its name
func (ts *TypeSet) Repr() string {
	return ts.Name
}

// copyTemplate for typesets will also copy the interface if necessary
func (ts *TypeSet) copyTemplate() DataType {
	newmembers := make([]DataType, len(ts.Members))

	for i, m := range ts.Members {
		newmembers[i] = m.copyTemplate()
	}

	var newinterf *TypeInterf = nil
	if ts.interf != nil {
		newmethods := make(map[string]*Method, len(ts.interf.Methods))

		for name, m := range ts.interf.Methods {
			newmethods[name] = &Method{
				FnType: m.FnType.copyTemplate(),
				Kind:   m.Kind,
			}
		}

		newinterf = &TypeInterf{Methods: newmethods}
	}

	return &TypeSet{
		Members: newmembers,
		interf:  newinterf,
		SetKind: ts.SetKind,
		Name:    ts.Name,
	}
}

// EnumMember represents a single, algebraic value
type EnumMember struct {
	Parent *TypeSet
	Name   string
	Values []DataType
}

func (em *EnumMember) equals(other DataType) bool {
	if oem, ok := other.(*EnumMember); ok {
		if !Equals(em.Parent, oem.Parent) || em.Name != oem.Name {
			return false
		}

		return TypeListEquals(em.Values, oem.Values)
	}

	return false
}

// enum members cannot accept coercion or casting
func (em *EnumMember) coerce(other DataType) bool {
	return false
}

func (em *EnumMember) cast(other DataType) bool {
	return false
}

// EnumMember's are the same type as their parent internally

// SizeOf an enum member is the size of its parent
func (em *EnumMember) SizeOf() uint {
	return em.Parent.SizeOf()
}

// AlignOf an enum is the alignment of its parent
func (em *EnumMember) AlignOf() uint {
	return em.Parent.AlignOf()
}

// Repr of an enum member reconstructs its repr in the type def
func (em *EnumMember) Repr() string {
	sb := &strings.Builder{}
	sb.WriteString(em.Name)
	sb.WriteRune('(')

	for i, v := range em.Values {
		sb.WriteString(v.Repr())

		if i < len(em.Values)-1 {
			sb.WriteString(", ")
		}
	}

	sb.WriteRune(')')

	return sb.String()
}

func (em *EnumMember) copyTemplate() DataType {
	emCopy := &EnumMember{Parent: em.Parent, Name: em.Name, Values: make([]DataType, len(em.Values))}

	for i, v := range em.Values {
		emCopy.Values[i] = v.copyTemplate()
	}

	return emCopy
}
