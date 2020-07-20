package common

import "github.com/ComedicChimera/whirlwind/src/types"

// Symbol represents a named value (stored locally or globally)
type Symbol struct {
	Name string
	Type types.DataType

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
	return s.DeclStatus == DSExported || s.DeclStatus == DSShared
}

// Import moves a symbol into a new package.  If it needs to update the
// DeclStatus, creates a new symbol with that statuses, if not simply acts as an
// identity (does not always copy).
func (s *Symbol) Import(exported bool) *Symbol {
	// if we are exported, nothing needs to change
	if exported {
		return s
	}

	// will always become a Remote import
	return &Symbol{
		Name: s.Name, Type: s.Type, Constant: s.Constant,
		DeclStatus: DSRemote, DefKind: s.DefKind,
	}
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
	SKindFuncDef
	SKindNamedValue
)
