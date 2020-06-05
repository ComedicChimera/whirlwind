package syntax

import (
	"fmt"
	"io"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// Parser is a recursive descent parser that generates a well-formed AST based
// on a valid set of input tokens.  It operates on a simple consume-reduce model
// where it consumes tokens until it encounters something it can safely reduce
// into an AST.  It handles ambiguity in some cases and also deals with
// whitespace.  Finally, the parser will parse as much code as its possible (ie.
// if it encounters a bad expression it will drop tokens until it reaches a safe
// state and then will continue parsing from there).  This allows it to mark
// multiple errors and give multiple pieces of feedback to the user.
type Parser struct {
	sc *Scanner

	// semantic stack is used to build the AST and accumulate tokens as necessary
	semanticStack []ASTNode

	// lookahead is used to store the parser's lookahead token for any calls of
	// consume that follow peek.  This property should never be accessed in
	// productions (it used to emulate the behavior of lookahead without
	// re-parsing tokens).  if this value is nil, there is no current lookahead.
	lookahead *Token

	// errorSuggestion is used to store any possible error suggestions.  Should
	// be cleared after it is used (automatically cleared by recovery state)
	errorSuggestion string

	// expectedIndentation is a variable used to store how far a given a line
	// should be indented.  It is mainly used to keep track of the current
	// expected indentation level (used by functions like expectIndent)
	expectedIndentation int

	// dependencies stores the names of all the top-level dependencies of the
	// current file.  They could be self-referential, internal, external, etc.
	// It just keeps tracks of them while it is parsing (used later in pkg asm)
	dependencies []string

	// recoveryMode indicates how the parser should attempt to recover if it
	// encounters a bad token.  This must be one of the enumerated modes below.
	recoveryMode int
}

const (
	// RMTopDef => Recover while in top-level definitions (not functions).
	// Parser should skip until it encounters another definition (all top-level)
	RMTopDef = iota

	// RMFuncDef => Recover in a function definition.  Parser skips until it
	// encounters an indentation level change and then skips until the end of
	// that indentation level.  If an arrow is encountered with an indentation,
	// it skips until the next newline.  Works for all functions (local & top).
	RMFuncDef

	// RMCplxExpr => Recover while in complex expression (with newlines).  Skip
	// until a valid closing token is reached in a different indentation level.
	RMCplxExpr

	// RMStmt => Recover while in statement.  Should skip until next newline.
	RMStmt

	// RMBlock => Recover while in block definition.  Skip until `do` is
	// encountered or until region with current indentation level.
	RMBlock

	// RMNone => No recovery possible
	RMNone
)

// readNoIndent reads tokens from the scanner until it encounters a token that
// is not an indent or it encounters an error.  If no error is encountered, the
// token is returned.  If an error is encountered, the error is returned.
func (p *Parser) readNoIndent() (*Token, error) {
	var err error

	for tok, err := p.sc.ReadToken(); err == nil; {
		if tok.Kind != INDENT {
			return tok, nil
		}
	}

	// if we reached this point, we can assume either no non-indent token was found
	// (ie. an EOF), or the parser encountered some form of scanner error.
	return nil, err
}

// consume moves the parser forward one token while safely respecting the lookahead.
// if the scanner encounters an error, it is returned (even if it is just an EOF).
// Moreover, consume skips all indentation tokens but does return newlines.
func (p *Parser) consume() (*Token, error) {
	// if there is no lookahead, just perform a normal read
	if p.lookahead == nil {
		return p.readNoIndent()
	}

	// if there is a lookahead, return that and clear the lookahead.
	// NOTE: indentation tokens never appear in the lookahead (filtered by peek)
	l := p.lookahead
	p.lookahead = nil
	return l, nil
}

// peek implements the parser's lookahead feature and returns the current value of
// the lookahead if one exists.  if not, it returns nil and an error.  This function
// will also never return an indent (they are skipped) but can peek a newline.
func (p *Parser) peek() (*Token, error) {
	ahead, err := p.readNoIndent()

	if err == nil {
		p.lookahead = ahead
		return ahead, nil
	}

	return nil, err
}

// these values are different possible expectation result states for the parser
const (
	ExpectOk = iota
	ExpectBad
	ExpectFatal
)

// expect consumes a token and if that token matches the expectation, ExpectOk
// is returned.  If the token does not match or is an EOF, ExpectBad is returned
// indicated that the current state is recoverable and that the parser should attempt
// recovery.  If another error occurs in the scanner, then ExpectFatal is returned
// indicating that the parser should stop parsing immediately.  Note: expect also
// logs any errors that occur (ie. as a result of an expectation failing)
func (p *Parser) expect(tokKind int) int {
	tok, err := p.consume()

	if err == nil {
		if tok.Kind == tokKind {
			return ExpectOk
		}

		p.unexpectedToken(tok)
		return ExpectBad
	} else if err == io.EOF {
		p.unexpectedEOF()
		return ExpectBad
	}

	util.LogMod.LogError(err)
	return ExpectFatal
}

// expectMany has similar behavior to expect except it matches on multiple kinds
// and returns the token kind it matched.  It also returns an expectation state
// and logs errors in a similar manner to expect.
func (p *Parser) expectMany(tokKinds ...int) (int, int) {
	tok, err := p.consume()

	if err == nil {
		for _, kind := range tokKinds {
			if tok.Kind == kind {
				return 0, ExpectOk
			}
		}

		p.unexpectedToken(tok)
		return 0, ExpectBad
	} else if err == io.EOF {
		p.unexpectedEOF()
		return 0, ExpectBad
	}

	util.LogMod.LogError(err)
	return 0, ExpectFatal
}

// expectIndent checks that the given indentation level is satisfied.  It should
// only be called after a newline.  If the indentation level is not matched, it
// returns a false along with the actual indentation level.  If the indentation
// level is matched, it returns true (the int value is meaningless).  If a scanner
// error is encountered, it is returned in its third return value.
func (p *Parser) expectIndent() (bool, int, error) {
	var err error
	level := 0

	for tok, err := p.sc.ReadToken(); err == nil; {
		if tok.Kind == INDENT {
			level++
		} else {
			return level == p.expectedIndentation, 0, nil
		}
	}

	// we only reach this point if an error occurs
	return false, 0, err
}

// recover is used to return the parser to a safe state defined by the current
// recovery mode.  It discards all tokens until this state is reached.  If the
// state is never reached or the recovery mode is set to none, this function
// returns false (it failed to recover).  Otherwise , it returns true.
func (p *Parser) recover() bool {
	switch p.recoveryMode {
	case RMTopDef:
		p.removeUntil(
			AKTypeDef, AKExportBlock, AKInterfBind,
			AKInterfDef, AKFuncDef, AKImport, AKVarDecl,
		)
		return p.recoverToDef()
	case RMStmt:
		p.removeUntil(AKSimpleStmt, AKBlockStmt, AKVarDecl)
		return p.recoverTo(NEWLINE)
	case RMBlock:
		p.removeUntil(AKSimpleStmt, AKBlockStmt, AKVarDecl)
		return p.recoverTo(DO)
		// TODO: RMCplxExpr, RMFuncDef
	}

	// RMNone is caught here
	return false
}

// recoverTo causes the parser to skip until it encounters a token of the given kind.
// If no such token is found, it returns false.  Otherwise, it returns true.
func (p *Parser) recoverTo(tokKind int) bool {
	for tok, err := p.peek(); err == nil; {
		if tok.Kind == tokKind {
			return true
		}

		// only consume if a recovery token was not encountered
		p.consume()
	}

	// EOF or token error causes recovery failure
	return false
}

// recoverToDef causes the parser to skip until it encounters a keyword
// indicating the beginnning of top-level definition.  Same mechanics as
// recoverTo (returns, logic, etc.)
func (p *Parser) recoverToDef() bool {
	for tok, err := p.peek(); err == nil; {
		switch tok.Kind {
		case FUNC, ASYNC, EXPORT, INTERF, TYPE, IMPORT, LET, CONST:
			return true
		}

		// only consume if a recovery token was not encountered
		p.consume()
	}

	// EOF or token error causes recovery failure
	return false
}

// removeUntil removes elements from the semantic stack until an ASTNode of one
// of the provided kinds is reached.  Used in recovery (deletes malformed nodes)
func (p *Parser) removeUntil(astKinds ...int) {
	for len(p.semanticStack) > 0 {
		last := p.semanticStack[len(p.semanticStack)-1]

		for _, kind := range astKinds {
			if kind == last.Kind() {
				return
			}
		}

		p.semanticStack = p.semanticStack[:len(p.semanticStack)-1]
	}
}

// unexpectedToken is used to log an unexpected token error
func (p *Parser) unexpectedToken(tok *Token) {
	err := util.NewWhirlErrorWithSuggestion(
		fmt.Sprintf("Unexpected Token `%s`", tok.Value),
		"Syntax",
		p.errorSuggestion,
		TextPositionOfToken(tok),
	)

	util.LogMod.LogError(err)
}

// unexpectedEOF logs an unexpected end of file error
func (p *Parser) unexpectedEOF() {
	err := util.NewWhirlError(
		"Unexpected End of File",
		"Syntax",
		nil, // EOFs only happen in one place :)
	)

	util.LogMod.LogError(err)
}
