package syntax

import (
	"errors"
	"fmt"
	"io"
	"strings"
)

// Parser is meant to be created once and reset for different parses (generates
// and stores one parsing table for repeated use)
type Parser struct {
	table         ParsingTable
	semanticStack []*ASTBranch
	scanner       *Scanner
	curr          *Token
	startSymbol   string
}

// NewParser loads in a grammar from the specified path and then creates an
// LL(1) parser for the grammar if possible (fails if grammar is ambiguous or
// cannot be loaded), should only be called once. note: uses globally defined
// start symbol
func NewParser(path string) (*Parser, error) {
	g, err := loadGrammar(path)

	if err != nil {
		return nil, err
	}

	pTable, err := createParsingTable(g)

	if err != nil {
		return nil, err
	}

	return &Parser{table: pTable, startSymbol: _startSymbol}, nil
}

// Parse a stream of tokens read from a scanner
func (p *Parser) Parse(sc *Scanner) (ASTNode, error) {
	// set the parser into its starting state
	p.scanner = sc
	if err := p.next(); err != nil {
		return nil, err
	}

	// runs the parsing algorithm beginning with the production of the start
	// symbol
	err := p.doParse(p.startSymbol)

	// handle parsing errors
	if err != nil {
		return nil, err
	}

	// if no error, return the first element of the semantic stack which should
	// be the entire AST (semantic stack should be cleaned up as the parsing
	// algorithm is running)
	return p.semanticStack[0], nil
}

// run the main parsing algorithm on a production
func (p *Parser) doParse(name string) error {
	// if the production has a $ prefix, it is an anonymous production and
	// should NOT appear on the resulting tree; otherwise, we can assume it is a
	// named production in which case we add a node for it
	if !strings.HasPrefix(name, "$") {
		p.semanticStack = append(p.semanticStack, &ASTBranch{Name: name})
	}

	// try and get the rule from the current token in the current production. if
	// no such rule exists, the token doesn't match to production and is
	// therefore to be marked as unexpected
	rule, ok := p.table[name][p.curr.Name]
	if !ok {
		return p.unexpectedToken()
	}

	// iterate through each grammatical element in the rule entry
	for _, item := range rule {
		switch item.Kind {
		case PTFTerminal:
			// if we have a terminal, we do a simple match to see if it is
			// valid. if it is, we push it onto the top node on the semantic
			// stack and read the next token from the scanner (and bubble an
			// errors that occur as a result); otherwise, we throw an unexpected
			// token error
			if p.curr.Name == item.Value {
				lastNdx := len(p.semanticStack) - 1
				p.semanticStack[lastNdx].Content = append(p.semanticStack[lastNdx].Content, (*ASTLeaf)(p.curr))

				if err := p.next(); err != nil {
					return err
				}
			} else {
				return p.unexpectedToken()
			}
		case PTFNonterminal:
			// if we have a nonterminal, we parse on the appopriate production.
			// if the parse is successful, we merge the branch created into the
			// branch before it on the semantic stack (thereby building the
			// tree); otherwise, we return the appropriate error
			err := p.doParse(item.Value)

			if err != nil {
				return err
			}

			lastNdx := len(p.semanticStack) - 1

			if len(p.semanticStack[lastNdx].Content) > 0 {
				p.semanticStack[lastNdx-1].Content = append(p.semanticStack[lastNdx-1].Content, p.semanticStack[lastNdx])
			}

			p.semanticStack = p.semanticStack[:lastNdx]
		case PTFEpsilon:
			// if the rule contains an epsilon at this point, we can assume that
			// the token is allowed not to match and we can carry on with
			// business as usual
			continue
		}
	}

	return nil
}

// reads a token for the scanner and stores it globally. produces an EOF token
// if the scanner returns an EOF errors, returns an error if the scanner returns
// something other than an EOF error (eg. malformed token error)
func (p *Parser) next() error {
	tok, err := p.scanner.ReadToken()

	if err != nil {
		if err == io.EOF {
			p.curr = &Token{Name: "$$"}
		} else {
			return err
		}
	} else {
		p.curr = tok
	}

	return nil
}

// reusable "factory" for unexpected token errors (returns unexpected end of
// file if the current token was an EOF token ie. had a name of '$$')
func (p *Parser) unexpectedToken() error {
	if p.curr.Name == "$$" {
		return errors.New("Unexpected end of file")
	}

	return fmt.Errorf("Unexpected token '%s' at (Ln: %d, Col: %d)", p.curr.Value, p.curr.Line, p.curr.Col)
}
