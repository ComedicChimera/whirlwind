package syntax

import (
	"bufio"
	"fmt"
	"io"
	"os"
	"strings"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// NewScanner creates a scanner for the given file
func NewScanner(fpath string) (*Scanner, error) {
	f, err := os.Open(fpath)

	if err != nil {
		return nil, err
	}

	s := &Scanner{file: bufio.NewReader(f), fpath: fpath, line: 1, currValid: true}
	return s, nil
}

// test if a rune is an ASCII character
func isLetter(r rune) bool {
	return r > '`' && r < '{' || r > '@' && r < '[' // avoid using <= and >= by checking characters on boundaries (same for isDigit)
}

// test if a rune is an ASCII digit
func isDigit(r rune) bool {
	return r > '/' && r < ':'
}

// Scanner works like an io.Reader for a file (outputting tokens)
type Scanner struct {
	file  *bufio.Reader
	fpath string

	line int
	col  int

	tokBuff []rune
	pos     int // store the position of the scanner (one ahead of the last scanned token)

	curr      rune
	currValid bool // tells us whether or not the scanner has hit an EOF (without checking output from readNext)
}

// ReadToken reads a single token from the stream, error can indicate malformed
// token or end of token stream
func (s *Scanner) ReadToken() (*Token, error) {
	for s.readNext() {
		malformed := false
		tok := &Token{}

		switch s.curr {
		// skip white space, line counting done on read int
		case ' ', '\t', '\n', '\r':
			continue
		// handle string-like
		case '"':
			tok, malformed = s.readStringLiteral()
		case '\'':
			tok, malformed = s.readCharLiteral()
		// handle comments
		case '/':
			p, more := s.peek()

			if !more {
				tok = s.getToken()
			} else if p == '/' {
				s.skipLineComment()
				continue
			} else if p == '*' {
				s.skipBlockComment()
				continue
			} else {
				tok = s.getToken()
			}
		// read in the '.', '..', and '...' tokens
		case '.':
			for i := 0; i < 2; i++ {
				p, more := s.peek()

				if more && p == '.' {
					s.readNext()
				}
			}

			tok = s.getToken()
		default:
			// check for identifiers
			if isLetter(s.curr) || s.curr == '_' {
				tok = s.readWord()
				// check numeric literals
			} else if isDigit(s.curr) {
				tok, malformed = s.readNumberLiteral()
				// handle compound operators
			} else if follows, ok := multiParticles[s.curr]; ok {
				// peek to see the follows
				p, more := s.peek()

				// if there are follows and they are accepted by the operator
				// compound them together
				if more && strings.Contains(follows, string(p)) {
					s.readNext()
				}

				tok = s.getToken()
				// simple, single token operators
			} else if _, ok := singleParticles[s.curr]; ok {
				tok = s.getToken()
			} else {
				// any other token must be malformed in some way
				malformed = true
			}
		}

		// discard the buff for the current scanned token
		s.discardBuff()

		// error out on any malformed tokens (along with contents of token
		// buffer)
		if malformed {
			return nil, util.NewWhirlError(
				fmt.Sprintf("Malformed Token \"%s\"", string(s.tokBuff)),
				&util.TextPosition{StartLn: s.line, StartCol: s.col, EndLn: s.line, EndCol: len(s.tokBuff)},
			)
		}

		return tok, nil
	}

	// end of file
	return nil, io.EOF
}

// create a token at the current position from the provided data
func (s *Scanner) makeToken(name string, value string) *Token {
	tok := &Token{Name: name, Value: value, Line: s.line, Col: s.col}
	s.col += len(value)

	return tok
}

// collect the current contents of the token buff into a string and create a
// token at the current position with a key and value both equal to the current
// contents of the token buffer
func (s *Scanner) getToken() *Token {
	tokValue := string(s.tokBuff)
	return s.makeToken(tokValue, tokValue)
}

// same behavior as get token with no arguments except it accepts a token name
func (s *Scanner) getTokenOf(name string) *Token {
	tokValue := string(s.tokBuff)
	return s.makeToken(name, tokValue)
}

// discards the current token buffer (as it is no longer being used)
func (s *Scanner) discardBuff() {
	s.tokBuff = s.tokBuff[:0] // keep buff allocated so we don't have to keep reallocating it everytime
}

// reads a rune from the file stream into the rune token content buffer and
// returns whether or not there are more runes to be read (true = no EOF, false
// = EOF),
func (s *Scanner) readNext() bool {
	r, _, err := s.file.ReadRune()

	if err != nil {
		if err == io.EOF {
			s.currValid = false
			return false
		}

		util.LogMod.LogFatal("Error reading file " + s.fpath)
	}

	// do line and column counting
	if r == '\n' {
		s.line++
		s.col = 0
	}

	s.tokBuff = append(s.tokBuff, r)
	s.curr = r
	s.pos++
	return true
}

// same behavior as readNext but doesn't populate the token buffer used for
// comments where it makes sense
func (s *Scanner) skipNext() bool {
	r, _, err := s.file.ReadRune()

	if err != nil {
		if err == io.EOF {
			s.currValid = false
			return false
		}

		util.LogMod.LogFatal("Error reading file " + s.fpath)
	}

	// do line and column counting
	if r == '\n' {
		s.line++
		s.col = 0
	}

	s.curr = r
	s.pos++
	return true
}

// peek a rune ahead on the scanner (used to test for malformed tokens) note
// that this functions peeks a single byte ahead and converts to a rune so if a
// more complex rune follows in the source text, the peek will not recognize it
// and instead return a possibly invalid utf-8 bit pattern
func (s *Scanner) peek() (rune, bool) {
	bytes, err := s.file.Peek(1)

	if err != nil {
		return 0, false
	}

	return rune(bytes[0]), true
}

// reads an identifier or a keyword from the input stream determines based on
// contents of stream (matches to all possible keywords)
func (s *Scanner) readWord() *Token {
	keywordValid := true

	// we know that whatever it started on was valid so we continue additionally
	// where know we are inside an identifier so we can allow numbers and _, use
	// a peek look ahead
	for c, more := s.peek(); more; s.readNext() {
		if isDigit(c) || c == '_' {
			keywordValid = false
		} else if !isLetter(c) {
			break
		}
	}

	tokValue := string(s.tokBuff)

	// note that invalid character at the end of tokBuff should both be
	// processed and not included in the token, return out of all of these
	// checks so that duplicate tokens aren't created if the check is successful
	// (found match)
	if keywordValid {
		if _, ok := keywords[tokValue]; ok {
			return s.makeToken(strings.ToUpper(tokValue), tokValue)
			// properly format token names of data types
		} else if _, ok := keywordDataTypes[tokValue]; ok {
			return s.makeToken(strings.ToUpper(tokValue)+"_TYPE", tokValue)
		} else {
			// handle special behavior of integral types
			for k, v := range integralTypes {
				if tokValue == k {
					return s.makeToken(strings.ToUpper(tokValue)+"_TYPE", tokValue)
				} else if tokValue == v+k {
					return s.makeToken(strings.ToUpper(k)+"_TYPE", tokValue)

				}
			}
		}
	}

	// assume that is just a pure identifier
	return s.makeToken("IDENTIFIER", tokValue)
}

// read in a floating point or integral number
func (s *Scanner) readNumberLiteral() (*Token, bool) {
	var isHex, isBin, isOct, isFloat, isUns, isLong bool

	// if we previous was an 'e' then we can expect a '-'
	expectNeg := false

	// if we triggered a floating point using '.' instead of 'e' than 'e' could
	// still be valid
	eValid := false

	// use loop break label to break out loop from within switch case
loop:

	// move forward at end of parsing to creating left overs in the token buff
	// (peek is not necessary here since we do still want to move forward each
	// iteration, just at the end)
	for ok := true; ok; ok = s.readNext() {
		// if we have identified signage or sign, then we are not expecting
		// anymore values and so exit out if an additional values are
		// encountered besides sign and size specifiers
		if isLong && isUns {
			break
		} else if isLong {
			if s.curr == 'u' {
				isUns = true
				continue
			} else {
				break
			}
		} else if isUns {
			if s.curr == 'l' {
				isLong = true
				continue
			} else {
				break
			}
		}

		// if we are expecting a negative and get another character then we
		// simply update the state (no longer expecting a negative) and continue
		// on (expect is not a hard expectation)
		if expectNeg && s.curr != '-' {
			expectNeg = false
		}

		// check to ensure that any binary literals are valid
		if isBin {
			if s.curr == '0' || s.curr == '1' {
				continue
			} else {
				break
			}
		}

		// check to ensure that any octal literals are valid
		if isOct {
			if s.curr > '/' && s.curr < '9' {
				continue
			} else {
				break
			}
		}

		if isDigit(s.curr) {
			continue
		}

		// check for validity of hex literal
		if isHex && (s.curr < 'A' || s.curr > 'F') && (s.curr < 'a' || s.curr > 'f') {
			break
			// after hitting floating point detector, we can only expect
			// numbers, 'e', and '-' and only under certain conditions
		} else if isFloat {
			switch s.curr {
			case 'e':
				if eValid {
					eValid = false
				} else {
					break loop
				}
			case '-':
				if expectNeg {
					// check if there is a non-number ahead then we actually
					// have 3 tokens and have to scan the other two separately
					pr, valid := s.peek()

					// hit EOF on peek, malformed token
					if !valid {
						return nil, true
					}

					// if it is not a digit, assume 3 separate tokens, continue
					// scanning after
					if !isDigit(pr) {
						break loop
					}

					expectNeg = false
				} else {
					break loop
				}
			default:
				break loop
			}
		}

		// determine token type based on token properties
		switch s.curr {
		case 'x':
			isHex = true
		case 'b':
			isBin = true
		case 'o':
			isOct = true
		case '.':
			isFloat = true
			eValid = true
		case 'e':
			isFloat = true
			expectNeg = true
		case 'u':
			isUns = true
		case 'l':
			isLong = true
		default:
			break
		}
	}

	// get the appropriate numeric literal name
	name := "INT_LITERAL"
	if isFloat {
		name = "FLOAT_LITERAL"
	} else if isBin {
		name = "BIN_LITERAL"
	} else if isHex {
		name = "HEX_LITERAL"
	} else if isOct {
		name = "OCT_LITERAL"
	}

	return s.getTokenOf(name), false
}

// read in a string literal
func (s *Scanner) readStringLiteral() (*Token, bool) {
	expectingEscape := false

	// no lookahead pattern necessary here
	for s.readNext() {
		// test for escape first
		if expectingEscape {
			// handle invalid escape sequences
			if s.readEscapeSequence() {
				expectingEscape = false
			} else {
				return nil, true
			}
		}

		if s.curr == '\\' {
			expectingEscape = true
			continue
		} else if s.curr == '"' {
			break
		}
	}

	// escape sequence occurred at end of file
	if expectingEscape {
		return nil, true
		// EOF occurred before end of string
	} else if s.tokBuff[len(s.tokBuff)-1] != '"' {
		return nil, true
	}

	return s.getTokenOf("STRING_LITERAL"), false
}

// read in a char literal
func (s *Scanner) readCharLiteral() (*Token, bool) {
	// if the char has no content then it is malformed
	if !s.readNext() {
		return nil, true
	}

	// if there is an escape sequence, read it and if it is invalid, char lit is
	// malformed
	if s.curr == '\\' && !s.readEscapeSequence() {
		return nil, true
	}

	// if the next token after processing the escape sequence is not a closing
	// quote than the char literal is too long on we are at EOF => malformed in
	// either case
	if !s.readNext() || s.curr != '\'' {
		return nil, true
	}

	// assume it is properly formed
	return s.getTokenOf("CHAR_LITERAL"), false
}

func (s *Scanner) readEscapeSequence() bool {
	if !s.readNext() {
		return false
	}

	invalidUEscape := func(r rune) bool {
		return !isDigit(r) && (r < 'A' || r > 'F') && (r < 'a' || r > 'f')
	}

	switch s.curr {
	case 'a', 'b', 'n', 'f', 'r', 't', 'v', '0', 's', '"', '\'', '\\':
		return true
	case 'u':
		for i := 0; i < 4; i++ {
			if !s.readNext() {
				return false
			}

			if invalidUEscape(s.curr) {
				return false
			}
		}
	case 'U':
		for i := 0; i < 8; i++ {
			if !s.readNext() {
				return false
			}

			if invalidUEscape(s.curr) {
				return false
			}

		}
	}

	return true
}

func (s *Scanner) skipLineComment() {
	for s.skipNext() && s.curr != '\n' {
	}
}

func (s *Scanner) skipBlockComment() {
	// skip opening '*'
	s.skipNext()

	for s.skipNext() {
		if s.curr == '*' {
			p, more := s.peek()

			if more && p == '/' {
				s.skipNext()
				return
			}
		}
	}
}
