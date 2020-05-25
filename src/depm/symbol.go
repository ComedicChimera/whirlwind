package depm

import "github.com/ComedicChimera/whirlwind/src/types"

// Symbol represents a named value (stored locally or globally)
type Symbol struct {
	Name string
	Type types.DataType

	// value properties

	// DeclStatus indicates where and how a symbol was declared.  Should be
	// filled with one of the declaration status constants listed below
	DeclStatus int

	// Kind indicates what type of value the symbol stores (typedef, binding,
	// etc.). Should contain one of the symbol kind constants listed below
	Kind int
}

// Declaration Statuses
const (
	DSLocal = iota
	DSInternal
	DSExported
	DSRemote // declared in another package, internal to this package
	DSShared // declared in another package, exported by this package
)

// Symbol Kinds
const (
	SKindTypeDef = iota
	SKindBinding
	SKindImport
	SKindFuncDef
)
