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
	CASE
	BREAK
	CONTINUE
	WHEN
	FINALLY
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

	// memory management
	VOL
	LOCAL
	NONLOCAL
	GLOBAL
	OWN
	MAKE
	REGION

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
	FROM

	// expression utils
	SUPER
	NULL
	IS
	AWAIT
	AS
	IN

	// whitespace
	NEWLINE
	INDENT
	DEDENT

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

	// null testing/null coalescion (?)
	NULLTEST

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
	ARROW

	// literals (and identifiers)
	IDENTIFIER
	STRINGLIT
	INTLIT
	FLOATLIT
	CHARLIT
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
	"finally":     FINALLY,
	"while":       WHILE,
	"fallthrough": FALLTHROUGH,
	"with":        WITH,
	"do":          DO,
	"of":          OF,
	"return":      RETURN,
	"yield":       YIELD,
	"vol":         VOL,
	"make":        MAKE,
	"local":       LOCAL,
	"nonlocal":    NONLOCAL,
	"global":      GLOBAL,
	"own":         OWN,
	"region":      REGION,
	"func":        FUNC,
	"async":       ASYNC,
	"variant":     VARIANT,
	"operator":    OPERATOR,
	"type":        TYPE,
	"closed":      CLOSED,
	"interf":      INTERF,
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
	"nothing":     NOTHING,
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
	"=":   ASSIGN,
	"?":   NULLTEST,
	".":   DOT,
	"..":  RANGETO,
	"...": ELLIPSIS,
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
	"::":  GETNAME,
	"=>":  ARROW,
	"<-":  BINDTO,

	// "/" is not scanned in as a symbol (to avoid comment conflicts) => entry is only here for grammar loading
	"/": DIVIDE,
}
