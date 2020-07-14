package semantic

import (
	"github.com/ComedicChimera/whirlwind/src/depm"
	"github.com/ComedicChimera/whirlwind/src/types"
)

// HIR stands for high-level IR.  It is a structural system used to represent a
// full Whirlwind program in well-structured, semantically annotated form.  At a
// top level, it is made up of a series of definition nodes which is what is
// described in this file.  For an explanation of how Whirlwind's predicates are
// described in the HIR, consult `hir_pred.go`.  However, this file does include
// the enumeration of node kinds.

// HIRRoot encloses the top level of any program file
type HIRRoot struct {
	Elements []HIRNode
}

// HIRNode is an interface implemented by all pieces of the HIR except for
// HIRRoot (which is more a scaffold for the program)
type HIRNode interface {
	// Should be one of the kinds enumerated below
	Kind() int
}

// This enum includes all of the kinds of nodes
const (
	NKTypeDef = iota
	NKInterfDef
	NKInterfBind
	NKFuncDef
	NKVarDecl
	NKGeneric
	NKVariantDef
	NKOperator
	NKBlockStmt
	NKSimpleStmt
	NKAssignment
	NKExpr // All values, names, and true expressions are NKExpr
)

// HIRTypeDef is the node used to represent a type definition.
type HIRTypeDef struct {
	// Symbol is all of the definition information about the symbol
	Symbol *depm.Symbol

	// FieldInits is a map of all field initializers along with what fields they
	// correspond to (used for type structs)
	FieldInits map[string]HIRNode
}

func (*HIRTypeDef) Kind() int {
	return NKTypeDef
}

// HIRInterfDef is the node used to represent an interface definition
type HIRInterfDef struct {
	Sym     *depm.Symbol
	Methods []HIRNode
}

func (*HIRInterfDef) Kind() int {
	return NKInterfDef
}

// HIRFuncDef is the node used to represent a function definition
type HIRFuncDef struct {
	Sym         *depm.Symbol
	Annotations map[string]string

	// Body can be `nil` if there is no function body
	Body HIRNode

	// ConstBody indicates whether or not the body is constant
	ConstBody bool
}

func (*HIRFuncDef) Kind() int {
	return NKFuncDef
}

// HIRVarDecl is used to represent a variable or constant declaration
type HIRVarDecl struct {
	Vars         []*depm.Symbol
	Initializers []HIRNode
}

func (*HIRVarDecl) Kind() int {
	return NKVarDecl
}

// HIRVariantDef is used to represent a variant definition
type HIRVariantDef struct {
	RootGeneric *depm.Symbol
	TypeParams  []types.DataType

	Body HIRNode
}

func (*HIRVariantDef) Kind() int {
	return NKVariantDef
}

// HIRGeneric is an enclosing node wrapping any generic definition
type HIRGeneric struct {
	Generic     *types.GenericType
	GenericNode HIRNode
}

func (*HIRGeneric) Kind() int {
	return NKGeneric
}

// HIRInterfBind represents an interface binding (generic bindings are
// just HIRInterfBinds wrapped in HIRGenerics)
type HIRInterfBind struct {
	// Symbol is anonymous: used to store aspects like DeclStatus
	Interf    *depm.Symbol
	BoundType types.DataType

	Methods []HIRNode
}

func (*HIRInterfBind) Kind() int {
	return NKInterfBind
}
