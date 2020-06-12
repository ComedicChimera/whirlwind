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

	s := &Scanner{file: bufio.NewReader(f), fpath: fpath, line: 1}
	return s, nil
}

// IsLetter tests if a rune is an ASCII character
func IsLetter(r rune) bool {
	return r > '`' && r < '{' || r > '@' && r < '[' // avoid using <= and >= by checking characters on boundaries (same for IsDigit)
}

// IsDigit tests if a rune is an ASCII digit
func IsDigit(r rune) bool {
	return r > '/' && r < ':'
}

// Scanner works like an io.Reader for a file (outputting tokens)
type Scanner struct {
	file  *bufio.Reader
	fpath string

	line int
	col  int

	tokBuilder strings.Builder

	curr rune

	indentLevel int

	// set after the scanner reads a newline to prompt it to update the
	// indentation level and produce tokens accordingly
	updateIndentLevel bool
}

// ReadToken reads a single token from the stream, error can indicate malformed
// token or end of token stream
func (s *Scanner) ReadToken() (*Token, error) {
	for s.readNext() {
		tok := &Token{}
		malformed := false

		switch s.curr {
		// skip spaces, carriage returns, and byte order marks
		case ' ', '\r', 65279:
			s.tokBuilder.Reset()
			continue
		// handle newlines where they are relevant
		case '\n':
			// line counting handled in readNext
			tok = s.getToken(NEWLINE)
			s.updateIndentLevel = true
		// handle indentation calculation
		case '\t':
			if s.updateIndentLevel {
				s.updateIndentLevel = false

				for s.readNext() && s.curr == '\t' {
				}

				level := s.tokBuilder.Len()
				s.tokBuilder.Reset()

				// rune value of first character of INDENT and DEDENT tokens
				// indicates by how much the level changed (parser should handle
				// interpreting this -- avoids creating a bunch of useless
				// INDENT and DEDENT tokens and weird control flow in ReadToken)
				levelDiff := level - s.indentLevel
				if levelDiff < 0 {
					s.indentLevel = level
					return s.makeToken(DEDENT, string(-levelDiff)), nil
				} else if levelDiff > 0 {
					s.indentLevel = level
					return s.makeToken(INDENT, string(levelDiff)), nil
				}
			} else {
				// drop the lingering tab
				s.tokBuilder.Reset()
			}

			continue
		// handle string-like
		case '"':
			tok, malformed = s.readStdStringLiteral()
		case '\'':
			tok, malformed = s.readCharLiteral()
		case '`':
			tok, malformed = s.readRawStringLiteral()
		// handle comments
		case '/':
			ahead, more := s.peek()

			if !more {
				tok = s.getToken(FDIVIDE)
			} else if ahead == '/' {
				s.skipLineComment()
				s.tokBuilder.Reset() // get rid of lingering `/`

				// return a newline so that the indentation is measured
				// correctly by the parser (position is still accurate)
				return &Token{Kind: NEWLINE, Value: "\n", Line: s.line, Col: s.col}, nil
			} else if ahead == '*' {
				s.skipBlockComment()
				s.tokBuilder.Reset() // get rid of lingering `/`
				continue
			} else {
				tok = s.getToken(FDIVIDE)
			}
		default:
			// check for identifiers
			if IsLetter(s.curr) || s.curr == '_' {
				tok = s.readWord()
			} else if IsDigit(s.curr) {
				// check numeric literals
				tok, malformed = s.readNumberLiteral()
			} else if kind, ok := symbolPatterns[string(s.curr)]; ok {
				// all compound tokens begin with valid single tokens so the
				// check above will match the start of any symbolic token

				// keep reading as long as our lookahead is valid: avoids
				// reading extra tokens (ie. in place of readNext)
				for ahead, more := s.peek(); more; {
					if skind, ok := symbolPatterns[s.tokBuilder.String()+string(ahead)]; ok {
						kind = skind
						s.readNext()
					} else {
						break
					}
				}

				// turn whatever we managed to read into a token
				tok = s.getToken(kind)
			} else {
				// any other token must be malformed in some way
				malformed = true
			}
		}

		// discard the built contents for the current scanned token
		s.tokBuilder.Reset()

		// error out on any malformed tokens (using contents of token builder)
		if malformed {
			return nil, util.NewWhirlError(
				fmt.Sprintf("Malformed Token \"%s\"", s.tokBuilder.String()), "Token",
				&util.TextPosition{StartLn: s.line, StartCol: s.col, EndLn: s.line, EndCol: s.col + s.tokBuilder.Len()},
			)
		}

		return tok, nil
	}

	// end of file
	return nil, io.EOF
}

// create a token at the current position from the provided data
func (s *Scanner) makeToken(kind int, value string) *Token {
	tok := &Token{Kind: kind, Value: value, Line: s.line, Col: s.col}
	s.col += len(value)

	return tok
}

// collect the contents of the token builder into a string and create a token at
// the current position with the provided kind and token string as its value
func (s *Scanner) getToken(kind int) *Token {
	tokValue := s.tokBuilder.String()
	return s.makeToken(kind, tokValue)
}

// reads a rune from the file stream into the token builder and returns whether
// or not there are more runes to be read (true = no EOF, false = EOF)
func (s *Scanner) readNext() bool {
	r, _, err := s.file.ReadRune()

	if err != nil {
		if err == io.EOF {
			return false
		}

		util.LogMod.LogFatal("Error reading file " + s.fpath)
	}

	// do line and column counting after the newline token
	// as been processed (so as to avoid positioning errors)
	if s.curr == '\n' {
		s.line++
		s.col = 0
	}

	s.tokBuilder.WriteRune(r)
	s.curr = r
	return true
}

// same behavior as readNext but doesn't populate the token builder used for
// comments where it makes sense
func (s *Scanner) skipNext() bool {
	r, _, err := s.file.ReadRune()

	if err != nil {
		if err == io.EOF {
			return false
		}

		util.LogMod.LogFatal("Error reading file " + s.fpath)
	}

	// do line and column counting after the newline token
	// as been processed (so as to avoid positioning errors)
	if s.curr == '\n' {
		s.line++
		s.col = 0
	}

	s.curr = r
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
	// if our word starts with an '_', it cannot be a keyword (simple check here)
	keywordValid := s.curr != '_'

	// to read a word, we assume that current character is valid and already in
	// the token builder (guaranteed by caller or previous loop cycle). we then
	// use a look-ahead to check if the next token will be valid. If it is, we
	// continue looping (and the logic outlined above holds). If not, we exit.
	// Additionally, if at any point in the middle of the word, we encounter a
	// digit or an underscore, we know we are not reading a keyword and set the
	// corresponding flag.  This function is never called on words that begin
	// with numbers so no need to check for first-character rules in it.
	for {
		c, more := s.peek()

		if !more {
			break
		} else if IsDigit(c) || c == '_' {
			keywordValid = false
		} else if !IsLetter(c) {
			break
		}

		s.readNext()
	}

	tokValue := s.tokBuilder.String()

	// if a keyword is possible and our current token value matches a keyword
	// pattern, create a new keyword token from the token builder
	if keywordValid {
		if kind, ok := keywordPatterns[tokValue]; ok {
			return s.makeToken(kind, tokValue)
		}
	}

	// otherwise, assume that it is just an identifier and act accordingly
	return s.makeToken(IDENTIFIER, tokValue)
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

	// move forward at end of parsing to creating left overs in the token
	// builder (peek is not necessary here since we do still want to move
	// forward each iteration, just at the end)
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

		if IsDigit(s.curr) {
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
					ahead, valid := s.peek()

					// hit EOF on peek, malformed token
					if !valid {
						return nil, true
					}

					// if it is not a digit, assume 3 separate tokens, continue
					// scanning after
					if !IsDigit(ahead) {
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
		case 'e', 'E':
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

	// binary, octal, decimal, and hexadecimal literals are all considered
	// integer literals and so the only decision here is whether or not to
	// create a floating point literal (use already accumulated information)
	if isFloat {
		return s.getToken(FLOATLIT), false
	}

	return s.getToken(INTLIT), false
}

// read in a standard string literal
func (s *Scanner) readStdStringLiteral() (*Token, bool) {
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
		} else if s.curr == '\n' {
			// catch newlines in strings
			return nil, true
		}
	}

	// escape sequence occurred at end of file
	if expectingEscape {
		return nil, true
		// EOF occurred before end of string
	} else if s.tokBuilder.String()[s.tokBuilder.Len()-1] != '"' {
		return nil, true
	}

	return s.getToken(STRINGLIT), false
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
	return s.getToken(CHARLIT), false
}

func (s *Scanner) readEscapeSequence() bool {
	if !s.readNext() {
		return false
	}

	readUnicodeSequence := func(count int) bool {
		for i := 0; i < count; i++ {
			if !s.readNext() {
				return false
			}

			r := s.curr

			if !IsDigit(r) && (r < 'A' || r > 'F') && (r < 'a' || r > 'f') {
				return false
			}
		}

		return true
	}

	switch s.curr {
	case 'a', 'b', 'n', 'f', 'r', 't', 'v', '0', 's', '"', '\'', '\\':
		return true
	case 'x':
		return readUnicodeSequence(2)
	case 'u':
		return readUnicodeSequence(4)
	case 'U':
		return readUnicodeSequence(8)
	}

	return true
}

// read in a raw string literal
func (s *Scanner) readRawStringLiteral() (*Token, bool) {
	for ok := true; ok; ok = s.curr == '`' {
		// catch incomplete raw string literals
		if !s.readNext() {
			return nil, true
		}
	}

	return s.getToken(STRINGLIT), false
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
