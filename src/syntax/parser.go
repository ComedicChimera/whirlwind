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

	// used to store the parser's stack of indentation frames.  The parser
	// starts with a frame on top of the stack to represent the frame of the
	// whole program.  This frame is indentation aware with an entry level of -1
	// (meaning it will never close => total enclosing frame).
	indentFrames []IndentFrame
}

// IndentFrame represents an indentation context frame.  An indentation context
// frame is a region of a particular kind of indentation processing: indentation
// blind (eg. inside `[]`) or indentation aware.  They are used to represent the
// contextual state of the parser as it relates to indentation.  As the parser
// switches between the two modes, frames are created and removed.  The parser
// decides when to switch and what to do when it switches based on the mode of
// the indentation frame and the entry level.  If it is in indentation aware
// mode, it exits when it returns to its entry level.  If it is not, it exits
// when all of the unbalanced openers (ie. `(`, `[`, and `{`) have been closed.
// When an indentation blind frame is closed, it updates the scanner's
// indentation level to be its entry level so that all changes to indentation
// inside the ignored block become irrelevant and the parser checks with the
// intended behavior.  A new indentation blind frame is created when an opener
// is encountered in an indentation aware frame.  A new indentation aware frame
// is created when an INDENT token is shifted in an indentation blind frame.
type IndentFrame struct {
	// mode == -1 => indentation aware
	// mode >= 0 => indentation blind, mode = number of unbalanced openers
	Mode int

	// the indentation level that the indentation frame began at
	EntryLevel int
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

	// parser starts with one indentation-aware indent frame that will never close
	return &Parser{ptable: parsingTable, indentFrames: []IndentFrame{IndentFrame{Mode: -1, EntryLevel: -1}}}, nil
}

// Parse runs the main parsing algorithm on the given scanner
func (p *Parser) Parse(sc *Scanner) (ASTNode, error) {
	p.sc = sc
	p.stateStack = append(p.stateStack, 0)

	// initialize the lookahead
	if err := p.consume(); err != nil {
		return nil, err
	}

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
				return p.semanticStack[0], nil
			}
		} else {
			switch p.lookahead.Kind {
			case NEWLINE:
				// unexpected newlines can be accepted whenever
				break
			// generate descriptive error messages for special tokens
			case EOF:
				return nil, util.NewWhirlError(
					"Unexpected End of File",
					"Syntax",
					nil, // EOFs only happen in one place :)
				)
			case INDENT, DEDENT:
				// check indentation changes only if we are not in a blind frame
				if p.topIndentFrame().Mode == -1 {
					// cache the original lookahead before we attempt to ignore
					// the indentation change based on the "empty" line rule
					originalLookahead := p.lookahead

					if err := p.consume(); err == nil && p.lookahead.Kind == NEWLINE {
						// if an indentation change (of any amount) is
						// immediately followed by a newline then we know that
						// the line is meaningless and the indentation change
						// can be ignored.  We adjust the scanner's indent level
						// by the indentation change denoted by the INDENT OR
						// DEDENT token to suppress/ignore the change
						if originalLookahead.Kind == INDENT {
							p.sc.indentLevel -= int(originalLookahead.Value[0])
						} else {
							p.sc.indentLevel += int(originalLookahead.Value[0])
						}
					} else {
						// if the parser cannot excuse the indent change, error
						return nil, util.NewWhirlError(
							"Unexpected Indentation Change",
							"Syntax",
							TextPositionOfToken(p.lookahead),
						)
					}

					// if there was no error, we can simply consume the next
					// token ("blank" lines can be ignored)
				}
			default:
				return nil, util.NewWhirlError(
					fmt.Sprintf("Unexpected Token `%s`", p.lookahead.Value),
					"Syntax",
					TextPositionOfToken(p.lookahead),
				)
			}

			// if we reach here, whatever token was errored on can be ignored
			if err := p.consume(); err != nil {
				return nil, err
			}
		}
	}
}

// shift performs a shift operation and returns an error indicating whether or
// not it was able to successfully get the next token (from scanner or self) and
// whether or not the shift operation on the current token is valid (ie. indents)
func (p *Parser) shift(state int) error {
	p.stateStack = append(p.stateStack, state)
	p.semanticStack = append(p.semanticStack, (*ASTLeaf)(p.lookahead))

	fmt.Println(p.lookahead.Kind)

	// used in all branches of switch (small operation - low cost)
	topFrame := p.topIndentFrame()

	switch p.lookahead.Kind {
	// handle indentation
	case INDENT, DEDENT:
		// implement frame control rules for indentation-aware frames
		if p.lookahead.Kind == INDENT && topFrame.Mode > -1 {
			p.pushIndentFrame(-1, p.sc.indentLevel-1)
		} else if p.lookahead.Kind == DEDENT && topFrame.Mode == -1 && topFrame.EntryLevel == p.sc.indentLevel {
			p.popIndentFrame()
		}

		levelChange := int(p.lookahead.Value[0])

		// no need to update lookahead if full level change hasn't been handled
		if levelChange > 1 {
			p.lookahead.Value = string(levelChange - 1)
			return nil
		}
	// handle indentation blind frame openers
	case LPAREN, LBRACE, LBRACKET:
		if topFrame.Mode == -1 {
			p.pushIndentFrame(1, p.sc.indentLevel)
		} else {
			topFrame.Mode++
		}
	// handle indentation blind frame closers
	case RPAREN, RBRACE, RBRACKET:
		// if there are closers, there must be openers so we can treat the
		// current frame as if we know it is a blind frame (because we do :D)
		if topFrame.Mode == 1 {
			p.popIndentFrame()
		} else {
			topFrame.Mode--
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
		copy(branch.Content, p.semanticStack[len(p.semanticStack)-rule.Count:])
		p.semanticStack = p.semanticStack[:len(p.semanticStack)-rule.Count]

		// calculate the added size of inlining all anonymous productions
		anonSize := 0

		// flag indicating whether or not any anonymous productions need to be
		// inlined (can't use anonSize since an anon production could contain a
		// single element which would mean no size change => anonSize = 0 even
		// though there would still be anonymous productions that need inlining)
		containsAnons := false

		// remove all empty trees and whitespace tokens from the new branch
		n := 0
		for _, item := range branch.Content {
			if subbranch, ok := item.(*ASTBranch); ok {
				if len(subbranch.Content) > 0 {
					branch.Content[n] = item
					n++

					// only want to inline anonymous production if they will
					// still exist after all empty trees are removed
					if subbranch.Name[0] == '$' {
						anonSize += len(subbranch.Content) - 1
						containsAnons = true
					}
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
		if containsAnons {
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

// topIndentFrame gets the indent frame at the top of the frame stack (ie. the
// current indent frame)
func (p *Parser) topIndentFrame() IndentFrame {
	return p.indentFrames[len(p.indentFrames)-1]
}

// pushIndentFrame pushes an indent frame onto the frame stack
func (p *Parser) pushIndentFrame(mode, level int) {
	p.indentFrames = append(p.indentFrames, IndentFrame{Mode: mode, EntryLevel: level})
}

// popIndentFrame removes an indent frame from the top of the frame stack
// (clears the current indent frame)
func (p *Parser) popIndentFrame() {
	p.indentFrames = p.indentFrames[:len(p.indentFrames)-1]
}
