package syntax

// Token represents a token read in by
// the scanner (and incorporated into
// the AST by an alias definition)
type Token struct {
	Name  string
	Value string
	Line  int
	Col   int
}

// token definitions (using maps for fast lookups)
var keywords = map[string]struct{}{
	// variable declarators
	"let":   struct{}{},
	"const": struct{}{},

	// control flow
	"if":          struct{}{},
	"elif":        struct{}{},
	"else":        struct{}{},
	"for":         struct{}{},
	"select":      struct{}{},
	"case":        struct{}{},
	"default":     struct{}{},
	"break":       struct{}{},
	"continue":    struct{}{},
	"when":        struct{}{},
	"after":       struct{}{},
	"loop":        struct{}{},
	"fallthrough": struct{}{},
	"with":        struct{}{},

	// function terminators
	"return": struct{}{},
	"yield":  struct{}{},

	// memory management
	"vol":    struct{}{},
	"make":   struct{}{},
	"own":    struct{}{},
	"delete": struct{}{},

	// function definitions
	"func":     struct{}{},
	"async":    struct{}{},
	"variant":  struct{}{},
	"operator": struct{}{},

	// type definitions
	"type":   struct{}{},
	"closed": struct{}{},
	"interf": struct{}{},

	// package keywords
	"import": struct{}{},
	"export": struct{}{},

	// expression utils
	"this":  struct{}{},
	"super": struct{}{},
	"null":  struct{}{},
	"is":    struct{}{},
	"await": struct{}{},
	"as":    struct{}{},
	"match": struct{}{},
	"to":    struct{}{},
	"vsize": struct{}{},
}

// store keyword type labels (separate so that token's name will be {VALUE}_TYPE)
// again, map for fast look ups
var keywordDataTypes = map[string]struct{}{
	"string": struct{}{},
	"float":  struct{}{},
	"bool":   struct{}{},
	"char":   struct{}{},
	"double": struct{}{},
	"any":    struct{}{},
}

// list of types followed by possible suffixes
var integralTypes = map[string]string{
	"int":   "u",
	"long":  "u",
	"short": "u",
	"byte":  "s",
}

// the particle followed by can follow it and be combined
var multiParticles = map[rune]string{
	'+': "+",
	'-': "->",
	'~': "*^/",
	'<': "-=",
	'>': "=",
	'=': "=>",
	'!': "=",
	':': ">=",
}

// store particles that have nothing following them (packaged as seen)
// map for fast lookups
var singleParticles = map[rune]struct{}{
	'(': struct{}{},
	')': struct{}{},
	'{': struct{}{},
	'}': struct{}{},
	'[': struct{}{},
	']': struct{}{},
	',': struct{}{},
	'#': struct{}{},
	'@': struct{}{},
	'?': struct{}{},
	'*': struct{}{},
	'%': struct{}{},
	'^': struct{}{},
	';': struct{}{},
	'|': struct{}{},
	'&': struct{}{},
}

// "." and "/" have special logic that determines their behavior
