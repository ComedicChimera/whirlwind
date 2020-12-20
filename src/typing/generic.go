package typing

// GenericType represents a generic definition.  Semantically, generics are not
// really considered types; however, it is implemented as one since many of our
// other data types may need to contain generics as well as other data types
// together (eg. interface methods).
type GenericType struct {
	// TypeParams is a map of shared references to WildcardTypes that stand in
	// for each of the type parameters of this generic.  A reference may appear
	// several times in the generic template if its type parameter is used
	// multiple times in the template.  All WildcardTypes correspondent to this
	// type parameter must share the same reference.
	TypeParams map[string]*WildcardType

	// Template is the data type that is used for generating the various
	// instances of this generic type.  It contains empty WildcardTypes that are
	// temporarily filled in and then copied.
	Template DataType
}

// WildcardType is a psuedo-type that is used as a stand-in for type parameters
// and as a part of the matching mechanism for interface binding.
type WildcardType struct {
	// Name stores the type parameter name this type is standing in for (eg.
	// `T`). It is primarily used for displaying out types (ie. implementing the
	// Repr()).
	Name string

	// Restrictors stores the list of types that this WildcardType can be
	// (correspondent to the restrictors on a type parameter).  This field will
	// be `nil` if no restrictors are applied.
	Restrictors []DataType

	// Value stores the type that has been filled in for the current type.  When
	// this field is not nil, the WildcardType is considered equivalent to this type
	// (and thus all coercions and casts will now be applied to the value).
	Value DataType
}
