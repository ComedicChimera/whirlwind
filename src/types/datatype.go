package types

// DataType is a general interface used to represent all data types provides
// basic characteristics of all types: coercion and casting (equality compares
// by identity)
type DataType interface {
	// both methods work in terms of other to self
	cast(other DataType) bool   // coercion checked before running
	coerce(other DataType) bool // equality checked before running
	equals(other DataType) bool // compare for exact equality and handle free types

	// Repr returns a string that represents the data type; should match type
	// label of data type wherever possible
	Repr() string

	// SizeOf determines the size of a data type in bytes
	SizeOf() uint

	// AlignOf determines the maximum possible alignment of the data type in
	// bytes (should be conservative)
	AlignOf() uint

	// copyTemplate creates a duplicate of the generic template assuming that
	// the placeholders have already been filled.  It only provides
	// implementations for the types that can be form into generics. On most
	// data types, this will simply copy the struct of the data type and fill
	// all of the DataType fields with the result of calling copyTemplate on
	// them
	copyTemplate() DataType
}

// Equals takes two types and determines if they are effectively identical to
// each other (note that types cannot provide custom definitions of equality and
// that for most types the function acts as an identity comparison.  However, on
// free types, it attempts to bind their values to whatever they are being
// compared to.
func Equals(a DataType, b DataType) bool {
	// TODO: initial free type check

	return a.equals(b)
}

// CoerceTo acts as wrapper to a types built in coercion function to the end of
// incorporating all of the baked in coercion logic common to all types (avoids
// redundancy and enables more tight control over coercion)
func CoerceTo(src DataType, dest DataType) bool {
	if Equals(src, dest) {
		return true
	}

	return dest.coerce(src)
}

// CastTo acts as a wrapper for a type's built in casting function much in the
// same way as CoerceTo: avoids redundancy and applies certain general rules
// before checking the specific casts validity (eg. interfaces) Note: not all
// types provide a meaningful casting function because they rely on the logic
// provided by this function thus why DataType.cast is not exposed
func CastTo(src DataType, dest DataType) bool {
	// if coercion succeeds, then casting will as well
	if CoerceTo(src, dest) {
		return true
	}

	// `any` can be cast to any other type
	if ps, ok := src.(PrimitiveType); ok && ps == PrimAny {
		return true
	}

	return dest.cast(src)
}
