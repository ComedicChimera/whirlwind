package common

import (
	"whirlwind/typing"
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
	NKOperDef // operator definition
	NKConsDef
	NKBlockStmt
	NKSimpleStmt
	NKAssignment
	NKExpr       // All values, names, and true expressions are NKExpr
	NKIncomplete // unwalked expression
)

// HIRTypeDef is the node used to represent a type definition.
type HIRTypeDef struct {
	Name string
	Type typing.DataType

	// FieldInits is a map of all field initializers along with what fields they
	// correspond to (used for type structs)
	FieldInits map[string]HIRNode
}

func (*HIRTypeDef) Kind() int {
	return NKTypeDef
}

// HIRInterfDef is the node used to represent an interface definition
type HIRInterfDef struct {
	Name    string
	Methods []HIRNode

	// Type is the actual, internal type of the interface, not any enclosing
	// generics -- this is more useful later on
	Type *typing.InterfType
}

func (*HIRInterfDef) Kind() int {
	return NKInterfDef
}

// HIRFuncDef is the node used to represent a function definition
type HIRFuncDef struct {
	Name        string
	Annotations map[string][]string

	// Type is the actual, internal type of the function, not any enclosing
	// generics -- this is more useful later on
	Type *typing.FuncType

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
	Generic         *typing.GenericType
	GenericNode     HIRNode
	Specializations []*typing.GenericSpecialization
}

func (*HIRGeneric) Kind() int {
	return NKGeneric
}

// HIRSpecialDef is used to represent a generic specialization
type HIRSpecialDef struct {
	RootGeneric *typing.GenericType
	TypeParams  []typing.DataType
	Body        HIRNode
}

func (*HIRSpecialDef) Kind() int {
	return NKSpecialDef
}

// HIRParametricSpecialDef represents a parametric generic specialization
type HIRParametricSpecialDef struct {
	RootGeneric *typing.GenericType
	TypeParams  []typing.DataType
	Body        HIRNode

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
	// Type is the actual, internal type of the interface binding, not any
	// enclosing generics -- this is more useful later on
	Type *typing.InterfType

	BoundType typing.DataType
	Methods   []HIRNode
}

func (*HIRInterfBind) Kind() int {
	return NKInterfBind
}

// HIROperDef represents an operator overload declaration
type HIROperDef struct {
	// OperKind is the token kind of the operator (subscript = `[`, slice = `:`)
	OperKind int

	// Signature is the function signature of the operator
	Signature *typing.FuncType

	Annotations map[string][]string
	Body        HIRNode

	Initializers map[string]HIRNode
}

func (*HIROperDef) Kind() int {
	return NKOperDef
}

// HIRConsDef represents a generic constraint definition.  This type exists
// mostly for compatability with existing compilation architecture
type HIRConsDef struct {
	Name     string
	ConsType typing.DataType
}

func (*HIRConsDef) Kind() int {
	return NKConsDef
}
