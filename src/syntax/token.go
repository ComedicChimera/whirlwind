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

	// control flow
	IF
	ELIF
	ELSE
	FOR
	SELECT
	CASE
	DEFAULT
	BREAK
	CONTINUE
	WHEN
	AFTER
	LOOP
	FALLTHROUGH
	WITH
	DO
	OF

	// function terminators
	RETURN
	YIELD

	// memory management
	VOL
	OWN
	MAKE
	DELETE

	// function definitions
	FUNC
	ASYNC
	OPERATOR
	VARIANT

	// type definitions
	TYPE
	INTERF
	CLOSED

	// package keywords
	IMPORT
	EXPORT

	// expression utils
	THIS
	SUPER
	NULL
	IS
	AWAIT
	AS
	MATCH
	TO
	IN

	// whitespace
	NEWLINE
	INDENT

	// type keywords
	STRING
	FLOAT
	BOOL
	CHAR
	DOUBLE
	ANY
	INT
	UINT
	LONG
	ULONG
	SHORT
	USHORT
	BYTE
	SBYTE

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
	COMPOSE

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

	// bitwise and memory operators
	AMP
	PIPE
	BXOR
	LSHIFT
	RSHIFT
	COMPL
	MOVE
	SET
	NULLTEST

	// dots
	DOT
	RANGETO
	VARARG

	// punctuation
	DECORAT
	ANNOTHASH
	LPAREN
	RPAREN
	LBRACE
	RBRACE
	LBRACKET
	RBRACKET
	COMMA
	SEMICOLON
	COLON

	// literals (and identifiers)
	IDENTIFIER
	STRINGLIT
	INTLIT
	FLOATLIT
	CHARLIT
	BOOLLIT
)

// token patterns (matching strings) for keywords
var keywordPatterns = map[string]int{
	"let":         LET,
	"const":       CONST,
	"if":          IF,
	"elif":        ELIF,
	"else":        ELSE,
	"for":         FOR,
	"select":      SELECT,
	"case":        CASE,
	"default":     DEFAULT,
	"break":       BREAK,
	"continue":    CONTINUE,
	"when":        WHEN,
	"after":       AFTER,
	"loop":        LOOP,
	"fallthrough": FALLTHROUGH,
	"with":        WITH,
	"do":          DO,
	"of":          OF,
	"return":      RETURN,
	"yield":       YIELD,
	"vol":         VOL,
	"make":        MAKE,
	"own":         OWN,
	"delete":      DELETE,
	"func":        FUNC,
	"async":       ASYNC,
	"variant":     VARIANT,
	"operator":    OPERATOR,
	"type":        TYPE,
	"closed":      CLOSED,
	"interf":      INTERF,
	"import":      IMPORT,
	"export":      EXPORT,
	"this":        THIS,
	"super":       SUPER,
	"null":        NULL,
	"is":          IS,
	"await":       AWAIT,
	"as":          AS,
	"match":       MATCH,
	"to":          TO,
	"in":          IN,
	"string":      STRING,
	"float":       FLOAT,
	"bool":        BOOL,
	"char":        CHAR,
	"double":      DOUBLE,
	"any":         ANY,
	"int":         INT,
	"uint":        UINT,
	"long":        LONG,
	"ulong":       ULONG,
	"short":       SHORT,
	"ushort":      USHORT,
	"byte":        BYTE,
	"sbyte":       SBYTE,
}

// token patterns for symbolic items - longest match wins
var symbolPatterns = map[string]int{
	"+":   PLUS,
	"++":  INCREM,
	"-":   MINUS,
	"--":  DECREM,
	"*":   STAR,
	"~/":  FDIVIDE,
	"%":   MOD,
	"~^":  RAISETO,
	"~*":  COMPOSE,
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
	"=":   SET,
	"?":   NULLTEST,
	".":   DOT,
	"..":  RANGETO,
	"...": VARARG,
	"@":   DECORAT,
	"#":   ANNOTHASH,
	"(":   LPAREN,
	")":   RPAREN,
	"{":   LBRACE,
	"}":   RBRACE,
	"[":   LBRACKET,
	"]":   RBRACKET,
	",":   COMMA,
	";":   SEMICOLON,
	":":   COLON,
}

// "/" has special logic that determines its behavior
