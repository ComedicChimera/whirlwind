package common

import (
	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// This file describes the predicates (expressions, blocks, statements, etc.)
// that are used to facilitate HIR's functionality at the lower-levels of
// program structure.  For a more full description of HIR's purpose and
// structure consult `hir_top.go`.

// HIRBlockStmt represents a specific kind of block (eg. for-loop, if-stmt,
// etc.).  It is also used to represent a block function body.
type HIRBlockStmt struct {
	// BlockKind must be one of the kinds in the enumeration below
	BlockKind int

	Header []HIRNode
	Body   []HIRNode
}

func (*HIRBlockStmt) Kind() int {
	return NKBlockStmt
}

// Enumeration of the kinds of block statements
const (
	// Loops
	BSForIter = iota
	BSCFor
	BSCondLoop
	BSInfLoop
	BSAsyncForIter
	BSNoBreak

	// Conditional Flow
	BSIfStmt
	BSElifStmt
	BSElseStmt
	BSIfTree // represents a tree of if-stmts, elif-stmts, and else-stmts

	// Pattern Matching
	MatchStmt
	CaseStmt

	// Context Management
	BSContextManager
	BSFinally
)

// HIRSimpleStmt represents a simple, keyword-based statement (ie. break,
// continue, return, etc)
type HIRSimpleStmt struct {
	// Must be one of the enumerated statement kinds
	StmtKind int

	// May be `nil` if the statement contains no elements
	Content []HIRExpr
}

func (*HIRSimpleStmt) Kind() int {
	return NKSimpleStmt
}

// Enumeration of the kinds of simple statements
const (
	SSKReturn = iota
	SSKYield
	SSKBreak
	SSKContinue
	SSKDelete
	SSKResize
	SSKFallthrough
	SSKFallMatch // fallthrough to match
)

// HIRVarDecl represents a variable declaration
type HIRVarDecl map[string]*DeclVar

// DeclVar represents a single variable in declaration
type DeclVar struct {
	Sym         *Symbol
	Initializer HIRExpr
	Volatile    bool
}

func (HIRVarDecl) Kind() int {
	return NKVarDecl
}

// HIRAssignment represents an assignment or move statement
type HIRAssignment struct {
	LHS []HIRNode

	// RHS will contain the unwrapped forms of compound assignment operators
	RHS []HIRNode

	// Must be one of the enumerated assignment kinds
	AssignKind int
}

func (*HIRAssignment) Kind() int {
	return NKAssignment
}

// Enumeration of the kinds of assignment
const (
	AKSet     = iota // `=`
	AKBind           // `<-`
	AKImpDecl        // `:=`
	AKMove           // `:>`
)

// HIRIncomplete is a node used to represent an unwalked predicate
type HIRIncomplete syntax.ASTBranch

func (*HIRIncomplete) Kind() int {
	return NKIncomplete
}
