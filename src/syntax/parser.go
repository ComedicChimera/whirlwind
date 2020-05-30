package syntax

import (
	"errors"
	"io"
	"strings"

	"github.com/ComedicChimera/whirlwind/src/util"
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

// NewParser attempts to load a preexisting parsing table for the given grammar
// path.  If no parsing table exists or the forceCreate flag is set to true,
// then the functions loads in a grammar from the specified path and then
// creates an LL(1) parser for the grammar if possible (fails if grammar is
// ambiguous or cannot be loaded). If the parsing table is created (by whatever
// circumstance), this function stores the created parsing table at the
// determined parsing table path for the given grammar.  This function should
// only be called once and uses a globally defined start symbol.
func NewParser(path string, forceCreate bool) (*Parser, error) {
	var pTable ParsingTable
	pTablePath := getParsingTablePath(path)

	if !forceCreate {
		if pTable, err := loadParsingTable(pTablePath); err == nil {
			return &Parser{table: pTable, startSymbol: _startSymbol}, nil
		}
	}

	g, err := loadGrammar(path)

	if err != nil {
		return nil, err
	}

	pTable, err = createParsingTable(g)

	if err != nil {
		return nil, err
	}

	saveParsingTable(pTablePath, pTable)

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
	// put the current production name on the semantic stack note: since
	// anonymous productions are inlined we can assume that any call to doParse
	// with be on a production that should be built into the final AST
	p.semanticStack = append(p.semanticStack, &ASTBranch{Name: name})

	// try and get the rule from the current token in the current production. if
	// no such rule exists, the token doesn't match to production and is
	// therefore to be marked as unexpected.  note: the rule is used as the
	// basis for the parsing queue (FIFO) for the current production thus the
	// name
	parsingQueue, ok := p.table[name][p.curr.Name]
	if !ok {
		return p.unexpectedToken()
	}

	// since items are removed from the queue as they are parsed, we can simply
	// loop until the queue has no more items left to parse and take the current
	// item to be the first element in the queue (since previous are sliced off)
	for len(parsingQueue) > 0 {
		item := parsingQueue[0]

		// we skip all epsilon rules during parsing so there is no case for them
		// note: since epsilons are never explicitly written in the actual
		// grammar; they tend to occur in anonymous productions and therefore
		// rarely even need to be skipped explicitly (they are just dropped
		// during inlining)
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
			// test for anonymous productions (ie. anonymously-named
			// nonterminals)
			if strings.HasPrefix(item.Value, "$") {
				// if the production is anonymous, we try to load its elements
				// and replace the anomymous production with them.
				anonRule, ok := p.table[item.Value][p.curr.Name]

				// if we could not match the token, we throw an error (like
				// above)
				if !ok {
					return p.unexpectedToken()
				}

				// if we can, we then inline the rule IF it contains more than
				// an epsilon. note: we avoid modifying the original production
				// rule with an implicit copy
				if len(anonRule) > 1 || anonRule[0].Kind != PTFEpsilon {
					parsingQueue = append(anonRule, parsingQueue[1:]...)

					// we now need to evaluate the new first term in queue and
					// so we avoid the slice at the end of the loop
					continue
				}

				// production was an epsilon rule and so we can simply move on
				// to the next element (small optimization)
			} else {
				// if we have a normal, named production we parse on the
				// appopriate production. if the parse is successful, we merge
				// the branch created into the branch before it on the semantic
				// stack (thereby building the tree); otherwise, we return the
				// appropriate error
				err := p.doParse(item.Value)

				if err != nil {
					return err
				}

				lastNdx := len(p.semanticStack) - 1
				lastTree := p.semanticStack[lastNdx]

				if len(lastTree.Content) > 0 {
					// remove trees that only contain one item to flatten the
					// tree structure: reduce the number of unnecessary nodes.
					// however, we want to preserve the start symbol nodes
					// (makes visiting easier)
					if len(lastTree.Content) == 1 && lastTree.Name != _startSymbol {
						p.semanticStack[lastNdx-1].Content = append(p.semanticStack[lastNdx-1].Content, lastTree.Content[0])
					} else {
						p.semanticStack[lastNdx-1].Content = append(p.semanticStack[lastNdx-1].Content, lastTree)
					}
				}

				p.semanticStack = p.semanticStack[:lastNdx]
			}
		}

		// slice off the first element (we have already evaluated it)
		parsingQueue = parsingQueue[1:]
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

	return util.NewWhirlError("Unexpected token '%s'", "Syntax", TextPositionOfToken(p.curr))
}
