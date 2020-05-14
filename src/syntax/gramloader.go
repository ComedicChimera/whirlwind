package syntax

import (
	"bufio"
	"errors"
	"fmt"
	"io"
	"log"
	"os"
)

// simple scanner/parser to read in and create grammar
type gramLoader struct {
	file    *bufio.Reader
	grammar Grammar
	curr    rune
}

// load the grammar using a gramLoader and return whether or not
// loading was successful (fails if grammar is syntactically invalid)
func loadGrammar(path string) (Grammar, error) {
	// open the file and check for errors
	f, err := os.Open(path)

	if err != nil {
		return nil, err
	}

	// create a gramLoader using bufio.Reader to load the file
	gl := &gramLoader{file: bufio.NewReader(f), grammar: make(map[string]Production)}

	// check for gramLoader errors
	err = gl.load()

	if err != nil {
		return nil, err
	}

	// return the loaded grammar if no errors
	return gl.grammar, nil
}

// load the grammar into a grammar struct
func (gl *gramLoader) load() error {
	for gl.next() {
		// ignore whitespace
		switch gl.curr {
		case ' ', '\n', '\t':
			break
		// handle comments (double / = ok, single = invalid)
		case '/':
			if b, berr := gl.peek(); berr == nil && b == '/' {
				gl.skipComment()
			} else {
				return gl.unexpectedToken()
			}
		// outer loading algorithm only expects whitespace, comments
		// and productions, so we only look to see if we have a valid beginning
		// to a production here instead of checking more generally
		default:
			if isLetter(gl.curr) {
				perr := gl.readProduction()

				if perr != nil {
					return perr
				}
			} else {
				return gl.unexpectedToken()
			}
		}

	}

	return nil
}

// read a rune from the stream and store it
func (gl *gramLoader) next() bool {
	r, _, err := gl.file.ReadRune()

	if err != nil {
		if err == io.EOF {
			return false
		}

		log.Fatal(err)
	}

	gl.curr = r
	return true
}

// peek and convert to rune if successful,
// return error if not (rune is 0 then)
func (gl *gramLoader) peek() (rune, error) {
	b, berr := gl.file.Peek(1)

	if berr != nil {
		return 0, berr
	}

	return rune(b[0]), nil
}

// read a line comment to its end (at a newline)
func (gl *gramLoader) skipComment() {
	for more := gl.next(); more && gl.curr != '\n'; gl.next() {
	}
}

// returns an unexpected token error
func (gl *gramLoader) unexpectedToken() error {
	return fmt.Errorf("Unexpected token '%c'", gl.curr)
}

// load and parse a production
func (gl *gramLoader) readProduction() error {
	// use a slice of runes to avoid excessive casting
	var prodName []rune

	// know first character is valid so use "do-while" pattern here
	for ok := true; ok; ok = gl.next() {
		// accepts letters and underscores in production name
		// collect them into the prodName slice of runes
		if isLetter(gl.curr) || gl.curr == '_' {
			prodName = append(prodName, gl.curr)
			// if we encounter a ':' we have reached end of production name
			// so we begin parsing content
		} else if gl.curr == ':' {
			// production is just group ending in ';'
			gelems, err := gl.parseGroupContent(';')

			// if there was a production error, return it
			if err != nil {
				return err
			}

			// otherwise, assume everything is fine,
			// add production to grammar and return
			gl.grammar[string(prodName)] = gelems
			return nil
			// all other runes here are invalid, so we mark as unexpected
		} else {
			return gl.unexpectedToken()
		}
	}

	// if we reach here, the loop did not exit properly (ran out of tokens)
	return errors.New("Unexpected EOF")
}

// parse a group or production to a closer
func (gl *gramLoader) parseGroupContent(expectedCloser rune) ([]GrammaticalElement, error) {
	var groupContent []GrammaticalElement

	for gl.next() {
		switch gl.curr {
		case ' ', '\t', '\n':
			// ignore whitespace
			continue
		// for groups and optionals, parse group just reads up to closing paren
		// so we just take its output and collect it into a group and push that
		// onto our group content stack
		case '(':
			gelems, err := gl.parseGroupContent(')')

			if err != nil {
				return nil, err
			}

			groupContent = append(groupContent, NewGroupingElement(GKindGroup, gelems))
		case '[':
			gelems, err := gl.parseGroupContent(']')

			if err != nil {
				return nil, err
			}

			groupContent = append(groupContent, NewGroupingElement(GKindOptional, gelems))
		case '*', '+':
			// repeaters must be applied to some form of grammatical element
			if len(groupContent) == 0 {
				return nil, errors.New("Unable to apply repeater to nothing")
			}

			lastNdx := len(groupContent) - 1

			if gl.curr == '*' {
				groupContent[lastNdx] = NewGroupingElement(GKindRepeat, groupContent[lastNdx:])
			} else {
				groupContent[lastNdx] = NewGroupingElement(GKindRepeatMultiple, groupContent[lastNdx:])
			}
		// alternators interrupt the current parsing group and create a new one to the same
		// closer so that they can combine the tailing elements with the elements before them.
		// if the tail is itself an alternator, then we combine them into a single alternator
		// over multiple values, if it is not, then we simply combine the two sets of group
		// elements across a single alternator.  because alternators interrupt parsing and bubble
		// upward, the above-described behavior can happen (alternators will only ever return
		// as the only element in their group so if a group starts with an alternator, we know that is all it is)
		case '|':
			tailContent, err := gl.parseGroupContent(expectedCloser)

			if err != nil {
				return nil, err
			}

			if tailContent[0].Kind() == GKindAlternator {
				alternator := tailContent[0].(AlternatorElement)
				alternator.PushFront(groupContent)
				return []GrammaticalElement{alternator}, nil
			}

			return []GrammaticalElement{NewAlternatorElement(groupContent, tailContent)}, nil
		case '\'':
			terminal, ok := gl.readTerminal()

			if !ok {
				return nil, errors.New("Malformed terminal")
			}

			groupContent = append(groupContent, Terminal(terminal))
		case expectedCloser:
			// if we encounter an empty production or group than we cannot close on it
			if len(groupContent) == 0 {
				return nil, errors.New("Unable to allow empty grammatical group")
			}

			return groupContent, nil
		default:
			// nonterminals only contain letters and underscores
			if isLetter(gl.curr) || gl.curr == '_' {
				nonTerminal := gl.readNonterminal()

				groupContent = append(groupContent, Nonterminal(nonTerminal))
				// if nothing else matched, then we have an unexpected token (some kind
				// of rogue particle or perhaps the residue of a malformed production or group
			} else {
				return nil, gl.unexpectedToken()
			}
		}
	}

	return nil, errors.New("Grammatical group not closed before EOF")
}

func (gl *gramLoader) readTerminal() (string, bool) {
	terminal := []rune{gl.curr}

	for gl.next() {
		if gl.curr == '\'' {
			return string(terminal), true
		}

		terminal = append(terminal, gl.curr)
	}

	// if the token is not closed before EOF, then it is malformed (and we reach here)
	return "", false
}

func (gl *gramLoader) readNonterminal() string {
	nonterminal := []rune{gl.curr}

	for gl.next() {
		if isLetter(gl.curr) || gl.curr == '_' {
			nonterminal = append(nonterminal, gl.curr)
		} else {
			break
		}
	}

	return string(nonterminal)
}
