package common

import (
	"github.com/ComedicChimera/whirlwind/src/typing"
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
	NKSpecialDef
	NKParametricSpecialDef
	NKOperDecl // operator overload
	NKBlockStmt
	NKSimpleStmt
	NKAssignment
	NKExpr       // All values, names, and true expressions are NKExpr
	NKIncomplete // unwalked expression
)

// HIRTypeDef is the node used to represent a type definition.
type HIRTypeDef struct {
	// Sym is all of the definition information about the symbol
	Sym *Symbol

	// FieldInits is a map of all field initializers along with what fields they
	// correspond to (used for type structs)
	FieldInits map[string]HIRNode
}

func (*HIRTypeDef) Kind() int {
	return NKTypeDef
}

// HIRInterfDef is the node used to represent an interface definition
type HIRInterfDef struct {
	Sym     *Symbol
	Methods []HIRNode
}

func (*HIRInterfDef) Kind() int {
	return NKInterfDef
}

// HIRFuncDef is the node used to represent a function definition
type HIRFuncDef struct {
	Sym         *Symbol
	Annotations map[string]string

	// Body can be `nil` if there is no function body
	Body HIRNode

	// Initializers contains all of the special modifiers to the arguments
	// of the function (ie. volatility, initializers)
	Initializers map[string]HIRNode
}

func (*HIRFuncDef) Kind() int {
	return NKFuncDef
}

// HIRGeneric is an enclosing node wrapping any generic definition
type HIRGeneric struct {
	Generic     *typing.GenericType
	GenericNode HIRNode
}

func (*HIRGeneric) Kind() int {
	return NKGeneric
}

// HIRSpecialDef is used to represent a generic specialization
type HIRSpecialDef struct {
	// RootGeneric can either be a function or interface depending on if this is
	// a function or method specialization
	RootGeneric *typing.GenericType

	TypeParams []typing.DataType
	Body       HIRNode
}

func (*HIRSpecialDef) Kind() int {
	return NKSpecialDef
}

// HIRParametricSpecialDef represents a parametric generic specialization
type HIRParametricSpecialDef struct {
	// RootGeneric can either be a function or interface depending on if this is
	// a function or method specialization
	RootGeneric *typing.GenericType

	TypeParams []typing.DataType
	Body       HIRNode

	// ParametricInstances is a shared slice of all the type parameters that
	// matched this specialization so that all necessary instances can be
	// generated.  The corresponding `typing.GenericSpecialization` has the
	// other reference
	ParametricInstances *[][]typing.DataType
}

func (*HIRParametricSpecialDef) Kind() int {
	return NKParametricSpecialDef
}

// HIRInterfBind represents an interface binding (generic bindings are
// just HIRInterfBinds wrapped in HIRGenerics)
type HIRInterfBind struct {
	// Symbol is anonymous: used to store aspects like DeclStatus
	Interf    *Symbol
	BoundType typing.DataType

	Methods []HIRNode
}

func (*HIRInterfBind) Kind() int {
	return NKInterfBind
}

// HIROperDecl represents an operator overload declaration
type HIROperDecl struct {
	// OperKind is the token value of the operator (subscript = `[`, slice = `:`)
	OperKind int

	// Signature is the function signature of the operator
	Signature *typing.FuncType

	Annotations map[string]string
	Body        HIRNode

	Initializers map[string]HIRNode
}

func (*HIROperDecl) Kind() int {
	return NKOperDecl
}
