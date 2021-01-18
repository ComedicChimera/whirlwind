package typing

// OpaqueType is used to represent a type that has yet to be defined but is
// needed to define other types.  It helps to facilitate mutual/cyclic
// dependency resolution between individual symbols
type OpaqueType struct {
	// EvalType stores the data type that is the evaluated/determined type for
	// this opaque type in much the same way that a WildcardType stores in an
	// internal type to be evaluated later
	EvalType DataType

	// DependsOn is a list of the names of symbol's that the definition this is
	// standing in place of depends on.  It is used to check whether or not the
	// accessing definition is a dependent type
	DependsOn map[string]struct{}

	// RequiresRef indicates whether dependent types should only use this type
	// as a reference element type (to prevent unresolveable recursive
	// definitions)
	RequiresRef bool
}

// All of OpaqueType's methods treat it as if it is it's evaluated type.  If it
// has yet to be evaluated, the only method that works is `Equals` since it can
// simply check if the definitions match exactly

// OpaqueGenericType is a special variant of the standard opaque type that works
// for generics.  It effectively creates links to all of the generate types so
// that any errors that occurred when creating the generate that were not
// detected because the generic did not yet exist can be rectified.  Unlike
// OpaqueType, this type is not meant to linger in the type system -- its is
// only used to facilitate opaque behavior and detect when an opaque generate is
// necessary.
type OpaqueGenericType struct {
	// EvalType works the same as it does in `OpaqueType` except it is
	// explicitly generic
	EvalType *GenericType

	// These two fields act the exact same as they do in `OpaqueType`
	DependsOn   map[string]struct{}
	RequiresRef bool

	// This field stores all of the generate instances that were created based
	// on this opaque type
	Generates []*OpaqueGenerateType
}

// OpaqueGenerateType is used as a temporary placeholder for a generate that is created
// based off of a generic opaque type.  These types can linger in the type system but
// are innately memoized to prevent generating unnecessary generics over and over.
type OpaqueGenerateType struct {
	// Generic stores a reference to the generic that this generate spawns from.  It
	// should be the same reference stored in the corresponding `OpaqueGenericType`
	Generic *GenericType

	// TypeParams is the slice of type parameters passed in to create this generate
	TypeParams []DataType

	// MemoizedGenerate stores the final/finished generate created after the
	// opaque generic is evaluated
	MemoizedGenerate DataType
}
