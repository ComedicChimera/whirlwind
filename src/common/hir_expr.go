package common

import (
	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// ---------------------------
// EXPRESSION COMPONENTS/UTILS
// ---------------------------

// This file contains the definitions relevant
// to expressions.  Note that the expression structure of Whirlwind
// is modeled after that of the Hindley-Milner type system (in terms of
// its basic constructs and operations) as follows:
// 	Var: `HIRName`
// 	App: `HIRApp`, `HIROperApp`
// 	Abs: `HIRLambda`
// Some additional extensions were added to better suit Whirlwind
//  Value:    `HIRValue`
//  Cast:     `HIRCast`     -- incorporates both casts and coercions
//  Match:    `HIRMatchExpr`
//  Extract:  `HIRExtract`  -- the with expression
//  Init:     `HIRInitList` -- initializer list
//  Generate: `HIRGenerate` -- used to denote the creation of a generic generate
//  Sequence: `HIRSequence` -- group/sequence of values (arrays, lists, dicts, tuples, vectors)
//  Slice:    `HIRSlice`    -- facilitates the slice operator (which is fairly complex)

// HIRExpr is an interface used by all expressions
type HIRExpr interface {
	// Type is the type of the expression result
	Type() types.DataType

	// Category is value category of the expression result
	Category() int

	// Constant indicates whether or not the expression result is constant
	Constant() bool
}

// ExprBase is a struct that all expressions and expression-like elements use as
// their base.  All of the properties are accessed via methods of HIRExpr which
// all types that use `ExprBase` implicitly implement
type ExprBase struct {
	dataType types.DataType

	// Category is the expression's value category -> should be one of the
	// enumerated categories below (using an int for extensibility)
	category int

	// Constant indicates whether or not the expression/value is constant
	constant bool
}

// Enumeration of the possible types of value categories
const (
	LValue = iota
	RValue
)

// Type of ExprBase is its dataType property
func (eb *ExprBase) Type() types.DataType {
	return eb.dataType
}

// Category of ExprBase is its category property
func (eb *ExprBase) Category() int {
	return eb.category
}

// Constant of ExprBase is its constant property
func (eb *ExprBase) Constant() bool {
	return eb.constant
}

// Kind of anything that uses ExprBase is NKExpr
func (eb *ExprBase) Kind() int {
	return NKExpr
}

// -------------------
// EXPRESSION ELEMENTS
// -------------------

// HIRMatchExpr represents an inline pattern match analysis
type HIRMatchExpr struct {
	ExprBase

	// Operand is the value being analyzed
	Operand HIRNode

	// Branches is a list of the branches where each branch represents a single
	// possible case (eg. `a, b => x` becomes `a => x, b => x`) This is ok b/c
	// HIRNode is a reference and so duplication costs little to nothing in
	// comparison with alternative approaches.
	Branches []struct {
		Case   HIRNode
		Result HIRNode
	}
}

// HIRExtract represents an monadic extraction expression (with expression)
type HIRExtract struct {
	ExprBase

	// Extractors is the list of monadic extractors in order (ie. the `x <- m`
	// statements in a with expression)
	Extractors []struct {
		DestSymbol *Symbol // the `x` in the above expr
		Source     HIRNode // the `m` in the above expr
	}

	// Result is the expression that is the result if this expression succeeds
	Result HIRNode
}

// HIRInitList represents a struct initializer list
type HIRInitList struct {
	// `ExprBase` contains the information about the returned struct type
	ExprBase

	// Source contains any struct that is being used for spread initialization
	// (may be `nil` if no such initialization is occurring)
	Source HIRNode

	// Initializers stores the initializers organized by field
	Initializers map[string]HIRNode
}

// HIRCast represents a type cast.  This could be an operator application but
// for semantic clarity, it is its own node (not to mention that it is only an
// operator in the loosest sense of the word).
type HIRCast struct {
	// The destination properties are included in `ExprBase`
	ExprBase

	// Source is the operand of the cast operator
	Source HIRNode
}

// HIRLambda represents a lambda definition
type HIRLambda struct {
	// All of the information about the function definition itself (ie. the
	// function type) is included in `ExprBase` (as the type of the expression)
	ExprBase

	// Lambda bodies cannot be constant (maybe?)
	Body HIRNode
}

// HIRSlice represents a slice operation
type HIRSlice struct {
	ExprBase

	// any of these may be null to indicate that the compiler should use the
	// default value (eg. `[:n]` would only fill in `End`)
	Begin, End, Step HIRNode
}

// HIROperApp represents an operator application (could be an operator overload,
// a builtin "overload" like `+` for ints, or a core operator like `.`).  NOTE:
// the conditional expression is treated as an operator application with 3 args
type HIROperApp struct {
	ExprBase

	// OperKind is the token kind of the operator being applied (`[` for subscript)
	OperKind int
	Operands []HIRNode
}

// HIRApp represents an a full function application
type HIRApp struct {
	ExprBase

	Function  *types.FuncType
	Arguments []HIRNode
}

// HIRSequence represents a sequence values
type HIRSequence struct {
	// Type of sequence implied in the expressions return type (in ExprBase)
	ExprBase

	// Dictionary values will be ordered in pairs
	Values []HIRNode
}

// HIRGenerate represents the implicit creation of a generic generate whenever a
// generic is accessed in an expression (special variant of Hindley-Milner's
// `var`)
type HIRGenerate struct {
	// `ExprBase` contains the generate returned in `dataType`
	ExprBase

	// Generic contains the generic source for the generate
	Generic *types.GenericType
}

// HIRName represents an identifier or variable access
type HIRName struct {
	ExprBase

	Name string

	// back-end can still point to specific names
	Position *util.TextPosition
}

// NewIdentifierFromSymbol creates a new HIRName for the given symbol
func NewIdentifierFromSymbol(sym *Symbol, pos *util.TextPosition) *HIRName {
	return &HIRName{
		ExprBase: ExprBase{dataType: sym.Type, constant: sym.Constant, category: LValue},
		Name:     sym.Name,
		Position: pos,
	}
}

// HIRValue represents a literal value or defined constant
type HIRValue struct {
	ExprBase

	Value string

	// back-end can also point to specific values
	Position *util.TextPosition
}

// NewLiteral creates a new HIRValue with the given type, value, and position
func NewLiteral(value string, dt types.DataType, pos *util.TextPosition) *HIRValue {
	return &HIRValue{
		ExprBase: ExprBase{dataType: dt, constant: false, category: RValue},
		Value:    value,
		Position: pos,
	}
}
