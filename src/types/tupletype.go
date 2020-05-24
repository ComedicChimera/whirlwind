package types

// TupleType is just a renamed list of data types since tuples have no
// additional modifiers
type TupleType []DataType

// NewTupleType get a new tuple type from a list of data types and adds its
// entry to the global type table
func NewTupleType(types []DataType) DataType {
	return newType(TupleType(types))
}

// tuples don't have any coercion logic
func (tt TupleType) coerce(dt DataType) bool {
	return false
}

// tuples can however be elementwise cast
func (tt TupleType) cast(dt DataType) bool {
	if ott, ok := dt.(TupleType); ok {
		if len(tt) != len(ott) {
			return false
		}

		for i, t := range tt {
			if !CastTo(t, ott[i]) {
				return false
			}
		}
	}

	return true
}

func (tt TupleType) equals(dt DataType) bool {
	if ott, ok := dt.(TupleType); ok {
		if len(tt) != len(ott) {
			return false
		}

		for i, t := range tt {
			if !Equals(t, ott[i]) {
				return false
			}
		}
	}

	return true
}

// Repr of a tuple is its reconstructed type label
func (tt TupleType) Repr() string {
	typeString := tt[0].Repr()

	for i := 1; i < len(tt); i++ {
		typeString += ", " + tt[i].Repr()
	}

	return "(" + typeString + ")"
}

// SizeOf a tuple is the size of a non-packed struct containing the types of a
// tuple (size of its IR)
func (tt TupleType) SizeOf() uint {
	return TypeListSize(tt)
}

// AlignOf a tuple is just the maximum alignment of its element types (padded as
// much as necessary)
func (tt TupleType) AlignOf() uint {
	return MaxAlign(tt)
}

func (tt TupleType) copyTemplate() DataType {
	newtuple := make([]DataType, len(tt))

	for i, v := range tt {
		newtuple[i] = v.copyTemplate()
	}

	return NewTupleType(newtuple)
}
