package syntax

import (
	"errors"
	"fmt"
	"io"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// Parser holds the state and provides the implementation for Whirlwind's
// parsing algorithm.  See the description of our algorithm above for details.
type Parser struct {
	sc *Scanner

	lookahead  *Token
	ptable     *ParsingTable
	stateStack []int

	// semantic stack is used to build the AST and accumulate tokens as necessary
	semanticStack []ASTNode

	// used to represent how many unexpected indentation tokens were encountered
	unbalancedIndent int
}

// NewParser creates a new parser from the scanner and the given grammar
func NewParser(grammarPath string, forceGBuild bool) (*Parser, error) {
	var parsingTable *ParsingTable

	if !forceGBuild {
		parsingTable, err := loadParsingTable(grammarPath)

		if err == nil {
			return &Parser{ptable: parsingTable}, nil
		}
	}

	g, err := loadGrammar(grammarPath)

	if err != nil {
		return nil, err
	}

	bnfg := expandGrammar(g)

	// clear the EBNF grammar from memory (no longer needed)
	g = nil

	parsingTable, ok := constructParsingTable(bnfg)

	// clear BNF grammar from memory (no longer needed)
	bnfg = nil

	if !ok {
		return nil, errors.New("Parser Error: Failed to build the parsing table")
	}

	// save the newly generate parsing table.  If it didn't generate, we want to
	// return an error (even if the parsing table generated correctly)
	if err = saveParsingTable(parsingTable, grammarPath); err != nil {
		return nil, err
	}

	return &Parser{ptable: parsingTable}, nil
}

// Parse runs the main parsing algorithm on the given scanner
func (p *Parser) Parse(sc *Scanner) (ASTNode, error) {
	p.sc = sc
	p.stateStack = append(p.stateStack, 0)

	// initialize the lookahead
	if err := p.consume(); err != nil {
		return nil, err
	}

loop:
	for {
		state := p.ptable.Rows[p.stateStack[len(p.stateStack)-1]]

		if action, ok := state.Actions[p.lookahead.Kind]; ok {
			switch action.Kind {
			case AKShift:
				if err := p.shift(action.Operand); err != nil {
					return nil, err
				}
			case AKReduce:
				p.reduce(action.Operand)
			case AKAccept:
				break loop
			}
		} else {
			switch p.lookahead.Kind {
			case NEWLINE:
				// unexpected newlines can be accepted whenever
				if err := p.consume(); err != nil {
					return nil, err
				}
				continue
			case INDENT:
				p.unbalancedIndent++
			case DEDENT:
				p.unbalancedIndent--
			// handle EOFs that are unexpected
			case EOF:
				return nil, util.NewWhirlError(
					"Unexpected End of File",
					"Syntax",
					nil, // EOFs only happen in one place :)
				)
			default:
				return nil, util.NewWhirlError(
					fmt.Sprintf("Unexpected Token `%s`", p.lookahead.Value),
					"Syntax",
					TextPositionOfToken(p.lookahead),
				)
			}

			// only cases that reach here are INDENT and DEDENT
			if len(p.lookahead.Value) > 1 {
				p.lookahead.Value = p.lookahead.Value[1:]
			} else {
				if err := p.consume(); err != nil {
					return nil, err
				}
			}
		}
	}

	return p.semanticStack[0], nil
}

// shift performs a shift operation and returns an error indicating whether or
// not it was able to successfully get the next token (from scanner or self) and
// whether or not the shift operation on the current token is valid (ie. indents)
func (p *Parser) shift(state int) error {
	p.stateStack = append(p.stateStack, state)
	p.semanticStack = append(p.semanticStack, (*ASTLeaf)(p.lookahead))

	// handle multiple indents
	if p.lookahead.Kind == INDENT || p.lookahead.Kind == DEDENT {
		// handle unbalanced indent
		if p.unbalancedIndent != 0 {
			if p.lookahead.Kind == INDENT {
				// check if our indent can compensate for our unbalanced dedent
				if p.unbalancedIndent < 0 && len(p.lookahead.Value) > -p.unbalancedIndent {
					p.lookahead.Value = p.lookahead.Value[:len(p.lookahead.Value)+p.unbalancedIndent-1]
					return nil
				}
			} else {
				// check if our dedent can compensate for our unbalanced indent
				if p.unbalancedIndent > 0 && len(p.lookahead.Value) > p.unbalancedIndent {
					p.lookahead.Value = p.lookahead.Value[:len(p.lookahead.Value)-p.unbalancedIndent-1]
					return nil
				}
			}

			return util.NewWhirlError(
				"Proper indentation level not restored before expected indent",
				"Syntax",
				TextPositionOfToken(p.lookahead),
			)
		}

		// handle normal case
		if len(p.lookahead.Value) > 1 {
			p.lookahead.Value = p.lookahead.Value[1:]
			return nil
		}
	}

	return p.consume()
}

// consume reads the next token from the scanner into the lookahead REGARDLESS
// OF WHAT IS IN THE LOOKAHEAD (not whitespace aware - should be used as such)
func (p *Parser) consume() error {
	tok, err := p.sc.ReadToken()

	if err == nil {
		p.lookahead = tok
		return nil
	} else if err == io.EOF {
		p.lookahead = &Token{Kind: EOF}
		return nil
	} else {
		return err
	}
}

// reduce performs a reduction and simplifies any elements it reduces
func (p *Parser) reduce(ruleRef int) {
	rule := p.ptable.Rules[ruleRef]

	// no need to perform any extra logic if we are reducing an epsilon rule
	if rule.Count == 0 {
		p.semanticStack = append(p.semanticStack, &ASTBranch{Name: rule.Name})
	} else {
		branch := &ASTBranch{Name: rule.Name, Content: make([]ASTNode, rule.Count)}
		copy(branch.Content, p.semanticStack[len(p.semanticStack)-rule.Count-1:])
		p.semanticStack = p.semanticStack[:len(p.semanticStack)-rule.Count]

		// remove all empty trees from the new branch
		n := 0
		for _, item := range branch.Content {
			if subbranch, ok := item.(*ASTBranch); ok {
				if len(subbranch.Content) > 0 {
					branch.Content[n] = item
					n++
				}
			}
		}

		branch.Content = branch.Content[:n]
		p.semanticStack = append(p.semanticStack, branch)
		p.stateStack = p.stateStack[:len(p.stateStack)-rule.Count]
	}

	// goto the next state
	currState := p.ptable.Rows[p.stateStack[len(p.stateStack)-1]]
	p.stateStack = append(p.stateStack, currState.Gotos[rule.Name])
}
