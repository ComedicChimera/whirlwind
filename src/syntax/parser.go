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
	// should be indented.  This value is only considered when the scanner
	// is not ignoring whitespace (ie. in a do block with an enclosing element)
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
	// Recover while in top-level definitions that aren't functions. Parser
	// should skip until it encounters another definition (all top-level)
	RMTopDef = iota

	// Recover in a function definition.  Parser should skip to next top-level
	// definition unless it encounters a `do` in which case, it skips until
	// the end of the indentation level and then skips until next top-level
	RMFuncDef

	// Recover while in complex expression (with newlines).  Skip until
	// a valid closing token is reached in a different indentation level.
	RMCplxExpr

	// Recover while in statement.  Should skip until next newline.
	RMStmt

	// Recover while in block definition.  Skip until `do` is encountered
	// or until region with current indentation level is encountered.
	RMBlock

	// No recovery possible
	RMNone
)

// consume moves the parser forward one token while safely respecting the lookahead.
// if the scanner encounters an error, it is returned (even if it is just an EOF)
func (p *Parser) consume() (*Token, error) {
	// if there is no lookahead, just perform a normal read
	if p.lookahead == nil {
		return p.sc.ReadToken()
	}

	// if there is a lookahead, return that and clear the lookahead
	l := p.lookahead
	p.lookahead = nil
	return l, nil
}

// peek implements the parser's lookahead feature and returns the current value of
// the lookahead if one exists.  if not, it returns nil and an error
func (p *Parser) peek() (*Token, error) {
	ahead, err := p.sc.ReadToken()

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

// recover is used to return the parser to a safe state defined by the current
// recovery mode.  It discards all tokens until this state is reached.  If the
// state is never reached or the recovery mode is set to none, this function
// returns false (it failed to recover).  Otherwise , it returns true.
func (p *Parser) recover() bool {
	// TODO
	return false
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
