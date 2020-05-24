package types

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
	members []DataType
	interf  *TypeInterf

	Name    string
	SetKind int
}

// NewTypeSet creates a new standard type set
func NewTypeSet(name string, sk int, members []DataType) DataType {
	return newType(&TypeSet{Name: name, SetKind: sk, members: members, interf: nil})
}

// NewInterface creates a new type set for a provided interface (creates an
// interface type)
func NewInterface(name string, interf *TypeInterf) DataType {
	return newType(&TypeSet{Name: name, SetKind: TSKindInterf, interf: interf})
}

// InstOf checks whether or not a data type is an element of the type set. if it
// is not explicitly an element of the type set or an element by proxy (ie.
// coerced into the type set), then it is not an instance of the type set
// regardless of quantifiers
func InstOf(elem DataType, set *TypeSet) bool {
	for _, dt := range set.members {
		if dt == elem {
			return true
		}
	}

	return false
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
	} else if ts.interf != nil && ts.interf.MatchType(other) {
		ts.members = append(ts.members, other)
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
		return TypeListEquals(ts.members, ots.members)
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
		return PointerSize * 3
	case TSKindEnum:
		return 2
	case TSKindInterf:
		return PointerSize * 4
	default:
		// TSKindUnion
		return MaxSize(ts.members)
	}
}

// AlignOf a type set follows similar semantics to SizeOf (see comment above for
// an enumeration of the underlying data structures by kind)
func (ts *TypeSet) AlignOf() uint {
	switch ts.SetKind {
	case TSKindAlgebraic:
		return PointerSize
	case TSKindEnum:
		return 2
	case TSKindInterf:
		return PointerSize
	default:
		// TSKindUnion
		return MaxAlign(ts.members)
	}
}

// Repr of a type set is its name
func (ts *TypeSet) Repr() string {
	return ts.Name
}

// copyTemplate for typesets will also copy the interface if necessary
func (ts *TypeSet) copyTemplate() DataType {
	newmembers := make([]DataType, len(ts.members))

	for i, m := range ts.members {
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

	return newType(&TypeSet{
		members: newmembers,
		interf:  newinterf,
		SetKind: ts.SetKind,
		Name:    ts.Name,
	})
}
