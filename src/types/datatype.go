package types

// DataType is general interface used to represent all
// data types provides basic characteristics of all types:
// coercion and casting (equality compares by identity)
type DataType interface {
	cast(other DataType) bool
	coerce(other DataType) bool
}

// Unify finds the unified type of a set if possible
// of data types (unified meaning type all types
// in the set are able to casted to: set != typeset here)
func Unify(dts ...DataType) (DataType, bool) {
	unifiedType := dts[0]

	for i := 1; i < len(dts); i++ {
		dt := dts[i]

		if CoerceTo(dt, unifiedType) {
			continue
		} else if CoerceTo(unifiedType, dt) {
			unifiedType = dt
		} else {
			return Generalize(dts...)
		}
	}

	return unifiedType, true
}

// Generalize finds the lowest type set that can
// accurately represent the types it is given.
// If no such type set exists, it returns nil, false
// Note: Mainly meant for use in Unification
func Generalize(dt ...DataType) (DataType, bool) {
	return nil, false
}

// InstOf checks whether or not a data type is a valid
// element of a type set both checking whether or not
// it is a preexisting element or whether or not it
// could be an element based on the quantifiers of the
// type set.  If no quantifiers exist and the data type
// is not already an element of the type set, it assumes
// that the type passed in is NOT an element of the type set
func InstOf(elem DataType, set DataType) bool {
	return false
}

// CoerceTo acts as wrapper to a types built in coercion
// function to the end of incorporating all of the
// baked in coercion logic common to all types (avoids
// redundancy and enables more tight control over coercion)
func CoerceTo(src DataType, dest DataType) bool {
	return false
}

// CastTo acts as a wrapper for a type's built in casting
// function much in the same way as CoerceTo: avoids
// redundancy and applies certain general rules before
// checking the specific casts validity (eg. interfaces)
// Note: not all types provide a meaningful casting
// function because they rely on the logic provided
// by this function thus why DataType.cast is not exposed
func CastTo(src DataType, dest DataType) bool {
	return false
}
