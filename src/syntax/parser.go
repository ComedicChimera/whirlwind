package syntax

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// Parser holds the state and provides the implementation for Whirlwind's
// parsing algorithm.  See the description of our algorithm above for details.
type Parser struct {
	sc *Scanner

	// semantic stack is used to build the AST and accumulate tokens as necessary
	semanticStack []ASTNode

	// dependencies stores the names of all the top-level dependencies of the
	// current file.  They could be self-referential, internal, external, etc.
	// It just keeps tracks of them while it is parsing (used later in pkg asm)
	dependencies []string

	// ignoreWhitespace is a state flag that can be set to prompt the parser
	// to skip all tokens with whitespace when this flag is set.  The prevIndent
	// member variable should be updated when this flag is set and compared to
	// the current indentation level of the scanner when it is cleared to check
	// for indentation errors outside of non-whitespace sensitive productions.
}

// unexpectedToken is used to log an unexpected token error
func (p *Parser) unexpectedToken(tok *Token) {
	err := util.NewWhirlError(
		fmt.Sprintf("Unexpected Token `%s`", tok.Value),
		"Syntax",
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
