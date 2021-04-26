package syntax

// Token represents a token read in by the scanner
type Token struct {
	Kind  int
	Value string
	Line  int
	Col   int
}

// The various kinds of a tokens supported by the scanner
const (
	// variables
	LET = iota
	CONST
	VOL

	// control flow
	IF
	ELIF
	ELSE
	FOR
	CASE
	BREAK
	CONTINUE
	WHEN
	NOBREAK
	WHILE
	FALLTHROUGH
	WITH
	DO
	OF
	MATCH
	TO

	// function terminators
	RETURN
	YIELD

	// function definitions
	FUNC
	ASYNC
	OPER
	SPECIAL

	// type definitions
	TYPE
	INTERF
	CLOSED
	CONSTRAINT

	// package keywords
	IMPORT
	EXPORT
	FROM

	// expression utils
	SUPER
	NULL
	IS
	AWAIT
	AS
	IN
	MAKE

	// whitespace
	NEWLINE
	INDENT
	DEDENT

	// type keywords
	U8
	U16
	U32
	U64
	I8
	I16
	I32
	I64
	F32
	F64
	STRING
	BOOL
	RUNE
	ANY
	NOTHING

	// arithmetic/function operators
	PLUS
	MINUS
	STAR
	DIVIDE
	FDIVIDE
	MOD
	RAISETO
	INCREM
	DECREM

	// boolean operators
	LT
	GT
	LTEQ
	GTEQ
	EQ
	NEQ
	NOT
	AND
	OR

	// bitwise operators
	AMP
	PIPE
	BXOR
	LSHIFT
	RSHIFT
	COMPL

	// assignment/declaration operators
	ASSIGN // =
	BINDTO // <-

	// dots
	DOT
	RANGETO
	ELLIPSIS

	// name access (::)
	GETNAME

	// punctuation
	ANNOTSTART
	LPAREN
	RPAREN
	LBRACE
	RBRACE
	LBRACKET
	RBRACKET
	COMMA
	SEMICOLON
	COLON
	ARROW

	// literals (and identifiers)
	IDENTIFIER
	STRINGLIT
	INTLIT
	FLOATLIT
	RUNELIT
	BOOLLIT

	// used in parsing algorithm
	EOF
)

// token patterns (matching strings) for keywords
var keywordPatterns = map[string]int{
	"let":         LET,
	"const":       CONST,
	"if":          IF,
	"elif":        ELIF,
	"else":        ELSE,
	"for":         FOR,
	"case":        CASE,
	"break":       BREAK,
	"continue":    CONTINUE,
	"when":        WHEN,
	"nobreak":     NOBREAK,
	"while":       WHILE,
	"fallthrough": FALLTHROUGH,
	"with":        WITH,
	"do":          DO,
	"of":          OF,
	"return":      RETURN,
	"yield":       YIELD,
	"vol":         VOL,
	"make":        MAKE,
	"func":        FUNC,
	"async":       ASYNC,
	"oper":        OPER,
	"special":     SPECIAL,
	"type":        TYPE,
	"closed":      CLOSED,
	"interf":      INTERF,
	"constraint":  CONSTRAINT,
	"import":      IMPORT,
	"export":      EXPORT,
	"from":        FROM,
	"super":       SUPER,
	"null":        NULL,
	"is":          IS,
	"await":       AWAIT,
	"as":          AS,
	"match":       MATCH,
	"to":          TO,
	"in":          IN,
	"i8":          I8,
	"i16":         I16,
	"i32":         I32,
	"i64":         I64,
	"u8":          U8,
	"u16":         U16,
	"u32":         U32,
	"u64":         U64,
	"f32":         F32,
	"f64":         F64,
	"string":      STRING,
	"bool":        BOOL,
	"rune":        RUNE,
	"any":         ANY,
	"nothing":     NOTHING,
}

// token patterns for symbolic items - longest match wins
var symbolPatterns = map[string]int{
	"+":   PLUS,
	"++":  INCREM,
	"-":   MINUS,
	"--":  DECREM,
	"*":   STAR,
	"//":  FDIVIDE,
	"%":   MOD,
	"**":  RAISETO,
	"<":   LT,
	">":   GT,
	"<=":  LTEQ,
	">=":  GTEQ,
	"==":  EQ,
	"!=":  NEQ,
	"!":   NOT,
	"&&":  AND,
	"||":  OR,
	"&":   AMP,
	"|":   PIPE,
	"^":   BXOR,
	"<<":  LSHIFT,
	">>":  RSHIFT,
	"~":   COMPL,
	"=":   ASSIGN,
	".":   DOT,
	"..":  RANGETO,
	"...": ELLIPSIS,
	"@":   ANNOTSTART,
	"(":   LPAREN,
	")":   RPAREN,
	"{":   LBRACE,
	"}":   RBRACE,
	"[":   LBRACKET,
	"]":   RBRACKET,
	",":   COMMA,
	";":   SEMICOLON,
	":":   COLON,
	"::":  GETNAME,
	"->":  ARROW,
	"<-":  BINDTO,
	"/":   DIVIDE,
}

// GetOperatorTokenValueByKind converts an operator token kind to a token value
// for displaying error messages regarding operators.  This function is very
// inefficient, but it is almost never used and by far the simplest way of going
// about our goal.
func GetOperatorTokenValueByKind(tokKind int) string {
	switch tokKind {
	case LBRACKET:
		return "[]"
	case COLON:
		return "[:]"
	default:
		for value, kind := range symbolPatterns {
			if kind == tokKind {
				return value
			}
		}
	}

	// unreachable
	return ""
}
