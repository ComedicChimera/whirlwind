package syntax

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
	"if":       struct{}{},
	"elif":     struct{}{},
	"else":     struct{}{},
	"for":      struct{}{},
	"select":   struct{}{},
	"case":     struct{}{},
	"default":  struct{}{},
	"break":    struct{}{},
	"continue": struct{}{},
	"when":     struct{}{},
	"after":    struct{}{},
	"match":    struct{}{},
	"to":       struct{}{},

	// function terminators
	"return": struct{}{},
	"yield":  struct{}{},

	// memory management
	"delete": struct{}{},
	"from":   struct{}{},
	"vol":    struct{}{},
	"make":   struct{}{},
	"static": struct{}{},
	"dyn":    struct{}{},
	"own":    struct{}{},

	// function definitions
	"func":        struct{}{},
	"async":       struct{}{},
	"variant":     struct{}{},
	"constructor": struct{}{},
	"operator":    struct{}{},

	// type definitions
	"type":   struct{}{},
	"struct": struct{}{},
	"interf": struct{}{},

	// package keywords
	"import": struct{}{},
	"export": struct{}{},

	// expression utils
	"this":  struct{}{},
	"super": struct{}{},
	"new":   struct{}{},
	"null":  struct{}{},
	"is":    struct{}{},
	"then":  struct{}{},
	"await": struct{}{},
	"value": struct{}{},
	"as":    struct{}{},
}

// store keyword type labels (separate so that token's name will be {VALUE}_TYPE)
// again, map for fast look ups
var keywordDataTypes = map[string]struct{}{
	"str":    struct{}{},
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
	'&': "&",
	'~': "*^/",
	'<': "-=",
	'>': "=",
	'=': "=>",
	'!': "=",
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
	':': struct{}{},
	',': struct{}{},
	'#': struct{}{},
	'@': struct{}{},
	'?': struct{}{},
	'*': struct{}{},
	'%': struct{}{},
	'^': struct{}{},
	';': struct{}{},
	'|': struct{}{},
}

// "." and "/" have special logic that determines their behavior
