package syntax

import (
	"errors"
	"fmt"
	"io"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// Parser is a modified LALR(1) parser designed for Whirlwind
type Parser struct {
	// scanner containing reference to the file being parsed
	sc *Scanner

	// standard LALR(1) parser state
	lookahead  *Token
	ptable     *ParsingTable
	stateStack []int

	// semantic stack is used to build the AST and accumulate tokens as necessary
	semanticStack []ASTNode
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
			// generate descriptive error messages for special tokens
			case EOF:
				return nil, util.NewWhirlError(
					"Unexpected End of File",
					"Syntax",
					nil, // EOFs only happen in one place :)
				)
			case INDENT:
				return nil, util.NewWhirlError(
					"Unexpected Indentation Increase",
					"Syntax",
					TextPositionOfToken(p.lookahead),
				)
			case DEDENT:
				return nil, util.NewWhirlError(
					"Unexpected Indentation Decrease",
					"Syntax",
					TextPositionOfToken(p.lookahead),
				)
			default:
				return nil, util.NewWhirlError(
					fmt.Sprintf("Unexpected Token `%s`", p.lookahead.Value),
					"Syntax",
					TextPositionOfToken(p.lookahead),
				)
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
		levelChange := int(p.lookahead.Value[0])

		// no need to update lookahead if full level change hasn't been handled
		if levelChange > 1 {
			p.lookahead.Value = string(levelChange - 1)
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

		// calculate the added size of inlining all anonymous productions
		anonSize := 0

		// remove all empty trees and whitespace tokens from the new branch
		n := 0
		for _, item := range branch.Content {
			if subbranch, ok := item.(*ASTBranch); ok {
				if len(subbranch.Content) > 0 {
					branch.Content[n] = item
					n++
				} else if subbranch.Name[0] == '$' {
					anonSize += len(subbranch.Content) - 1
				}
			} else if leaf, ok := item.(*ASTLeaf); ok {
				switch leaf.Kind {
				case NEWLINE, INDENT, DEDENT:
					continue
				default:
					branch.Content[n] = item
					n++
				}
			}
		}

		branch.Content = branch.Content[:n]

		// If no anonymous productions need to be inlined, slice the branch down
		// to the appropriate length and continue executing.  If anonymous
		// productions do need to inlined, allocate a new slice to store the new
		// branch and copy/inline as necessary (could do some trickery with
		// existing slice but that seems unnecessarily complex /shrug)
		if anonSize > 0 {
			newBranchContent := make([]ASTNode, anonSize+n)
			k := 0

			for _, item := range branch.Content {
				if subbranch, ok := item.(*ASTBranch); ok && subbranch.Name[0] == '$' {
					for _, subItem := range subbranch.Content {
						newBranchContent[k] = subItem
						k++
					}
				} else {
					newBranchContent[k] = item
					k++
				}
			}

			branch.Content = newBranchContent
		}

		p.semanticStack = append(p.semanticStack, branch)
		p.stateStack = p.stateStack[:len(p.stateStack)-rule.Count]
	}

	// goto the next state
	currState := p.ptable.Rows[p.stateStack[len(p.stateStack)-1]]
	p.stateStack = append(p.stateStack, currState.Gotos[rule.Name])
}
