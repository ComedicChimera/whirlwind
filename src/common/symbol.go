package common

import (
	"whirlwind/typing"
)

// Symbol represents a named value (stored locally or globally)
type Symbol struct {
	Name string
	Type typing.DataType

	// Constant indicates that the symbol is defined as constant
	Constant bool

	// no need to store a category: all symbols are LValues by definition

	// DeclStatus indicates where and how a symbol was declared.  Should be
	// filled with one of the declaration status constants listed below
	DeclStatus int

	// DefKind indicates what type of value the symbol stores (typedef, binding,
	// etc.). Should contain one of the symbol kind constants listed below
	DefKind int
}

// VisibleExternally determines if remote packages can access this symbol
func (s *Symbol) VisibleExternally() bool {
	// Since remote symbols are stored in the local table, they won't be able to
	// be imported directly, but they can still be used in exported definitions
	// without causing unnecessary errors
	return s.DeclStatus == DSExported || s.DeclStatus == DSRemote
}

// Declaration Statuses
const (
	DSLocal = iota
	DSInternal
	DSExported
	DSRemote // declared in another package, internal to this package
)

// Definition Kinds
const (
	DefKindTypeDef = iota
	DefKindBinding
	DefKindFuncDef
	DefKindNamedValue
)

// OpaqueSymbol acts as a shared opaque symbol references during cyclic
// resolution.  One of these references should be created and distributed to all
// walkers in resolution unit.  Then, the contents of this reference should be
// updated as the opaque reference changes.
type OpaqueSymbol struct {
	Name string

	// This is used to determine whether or not this symbol should be visible in
	// the current package as well as whether or not when a definition is
	// complete if it should be updated by that finished definition (only if in
	// same package).
	SrcPackageID uint

	// Type can be any one of the opaque types
	Type typing.DataType

	// DependsOn is a list of the names of symbol's that the definition this is
	// standing in place of depends on.  It is used to check whether or not the
	// accessing definition is a dependent type.  The key is the name of the
	// symbol and the value is the list of packages that this symbol is defined
	// in (eg. if a data type uses two symbols by the same name defined in
	// different packages, you would have two package IDs)
	DependsOn map[string][]uint

	// RequiresRef indicates whether dependent types should only use this type
	// as a reference element type (to prevent unresolveable recursive
	// definitions)
	RequiresRef bool
}
