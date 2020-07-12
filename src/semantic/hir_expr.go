package semantic

// This file contains the definitions relevant
// to expressions.  Note that the expression structure of Whirlwind
// is modeled after that of the Hindley-Milner type system (in terms of
// its basic constructs and operations) as follows:
// 	Var: `HIRName`
// 	App: `HIRApp`, `HIROperApp`
// 	Abs: `HIRLambda`
// Some additional extensions were added to better suit Whirlwind
//  Value:   `HIRValue`
//  Cast:    `HIRCast` -- incorporates both casts and coercions
//  Match:   `HIRMatchExpr`
//  Extract: `HIRExtract` -- the with expression

// HIRExpr is an interface used by all expressions
type HIRExpr interface {
	// Must be one of the enumerated expr kinds below
	ExprKind() int
}

// Enumeration of the kinds of expressions
const (
	EKName = iota
	EKApp
	EKOper // operator application
	EKLambda
	EKValue
	EKCast
	EKMatch   // match expression
	EKExtract // with expression
)
