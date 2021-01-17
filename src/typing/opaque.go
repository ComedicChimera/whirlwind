package typing

// TODO: how do we make this work with generics?

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
