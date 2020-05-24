package types

import "reflect"

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

// PointerSize is a variable storing the size of a pointer for architecture
// being compiled for.  This variable must be set by the compiler before it
// begins compiling
var PointerSize uint = 0

// TypeInfo is a data structure unique to each data type representing any type
// information shared between data types and instances (should only be created
// once). this information must be stored in the type table (each type must have
// a type info entry).
type TypeInfo struct {
	Interf *TypeInterf
	// TODO: Type structs
}

// the type table represents a common storage place for all type references so
// that each data type is only defined once and any modifications to that type
// propagate to all known "instances" of that type.  It also stores an necessary
// type information (such as interfaces) so that this information can be
// accessed and modified in a similar manner to that of the type itself.  For
// these reasons, any NewType() methods should always return an entry in this
// table.
var typeTable = make(map[DataType]*TypeInfo)

// newType is a common function that should be included in the new type methods
// of any data type so as to ensure that the type is properly added to the type
// table or retrieved if it already exists (such a check should be performed for
// all usages)
func newType(dt DataType) DataType {
	for entry := range typeTable {
		if reflect.DeepEqual(dt, entry) {
			return entry
		}
	}

	typeTable[dt] = &TypeInfo{}
	return dt
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
	return false
}

// CastTo acts as a wrapper for a type's built in casting function much in the
// same way as CoerceTo: avoids redundancy and applies certain general rules
// before checking the specific casts validity (eg. interfaces) Note: not all
// types provide a meaningful casting function because they rely on the logic
// provided by this function thus why DataType.cast is not exposed
func CastTo(src DataType, dest DataType) bool {
	return false
}
