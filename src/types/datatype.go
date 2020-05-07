package types

import "reflect"

// NOTE: equality between data types can be tested using
// `==` because although the data types are pointers stored
// in interfaces, they are pointers to singular references
// created once and stored in the type table and so since
// each type should have exactly one address, this works

// DataType is a general interface used to represent all
// data types provides basic characteristics of all types:
// coercion and casting (equality compares by identity)
type DataType interface {
	cast(other DataType) bool
	coerce(other DataType) bool
}

// TypeInfo is a data structure unique to each data type
// representing any type information shared between
// data types and instances (should only be created once).
// this information must be stored in the type table (each
// type must have a type info entry).
type TypeInfo struct {
	PackageName string
}

// the type table represents a common storage place
// for all type references so that each data type is only
// defined once and any modifications to that type propagate
// to all known "instances" of that type.  It also stores
// an necessary type information (such as interfaces) so that
// this information can be accessed and modified in a similar
// manner to that of the type itself.  For these reasons, any
// NewType() methods should always return an entry in this table.
var typeTable = make(map[DataType]*TypeInfo)

// newType is a common function that should be included in the
// new type methods of any data type so as to ensure that the
// type is properly added to the type table or retrieved if it
// already exists (such a check should be performed for all usages)
func newType(dt DataType, pkgName string) DataType {
	for entry, ti := range typeTable {
		if reflect.TypeOf(entry) == reflect.TypeOf(dt) && reflect.ValueOf(dt).Elem() == reflect.ValueOf(entry).Elem() {
			if ti.PackageName == pkgName {
				return entry
			}
		}
	}

	typeTable[dt] = &TypeInfo{PackageName: pkgName}
	return dt
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
