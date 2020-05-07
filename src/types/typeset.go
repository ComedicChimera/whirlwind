package types

// TypeSet represents a polymorphic type that
// can be any of the types included in itself
// (eg. algebraic typesets, interfaces, etc.)
type TypeSet struct {
	types       []DataType
	quantifiers []func(DataType) bool
}

// InstOf checks whether or not a data type is an
// element of the type set. if it is not explicitly
// an element of the type set or an element by proxy
// (ie. coerced into the type set), then it is not
// an instance of the type set regardless of quantifiers
func InstOf(elem DataType, set *TypeSet) bool {
	for _, dt := range set.types {
		if dt == elem {
			return true
		}
	}

	return false
}

// coecion on type sets follows three simple rules:
// - if the other is an element of the type set, then
// it can be coerced to the type set.
// - if the type set has no quantifiers and the element
// is not already an element of the type set, then
// it cannot be coerced to the type set
// - if the type set does have quantifiers and the
// input satisfied the quantifiers and is not element
// of the type set, then it is coercible to the type set
// AND is now considered an element of the type set (by proxy)
func (ts *TypeSet) coerce(other DataType) bool {
	if InstOf(other, ts) {
		return true
	} else if len(ts.quantifiers) == 0 {
		return false
	} else {
		for _, q := range ts.quantifiers {
			if !q(other) {
				return false
			}
		}

		ts.types = append(ts.types, other)
		return true
	}
}

// casting follows the same rules as coercion
func (ts *TypeSet) cast(other DataType) bool {
	return ts.coerce(other)
}
