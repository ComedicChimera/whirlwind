package syntax

import "fmt"

type Parser struct {
	grammar       Grammar
	tokens        []Token
	semanticStack []ASTNode
	errorPosition int
}

type ParseError struct {
	Tok Token
}

func (pe ParseError) Error() string {
	return fmt.Sprintf("Unexpected Token '%s' at (Ln: %d, Col: %d)", pe.Tok.Value, pe.Tok.Line, pe.Tok.Col+1)
}

func NewParser(grammar Grammar, tokens []Token) *Parser {
	return &Parser{grammar: grammar, tokens: tokens, errorPosition: -1}
}

func (p *Parser) Parse() error {
	return nil
}
