package build

import (
	"fmt"
	"strings"

	"whirlwind/logging"
	"whirlwind/syntax"
)

// preprocessFile reads the first few tokens of a file to extract any compiler
// metadata and decide whether or not to compile the file
func (c *Compiler) preprocessFile(sc *syntax.Scanner) (map[string]string, bool) {
	next, ok := sc.ReadToken()
	if !ok {
		return nil, false
	}

	// if we encountered metadata
	if next.Kind == syntax.NOT {
		return c.readMetadata(sc)
	}

	// if the scanner has not populated to lookahead, we can just store the
	// token we read in there
	sc.UnreadToken(next)

	return nil, true
}

// readMetadata reads and processes metadata at the start of a file
func (c *Compiler) readMetadata(sc *syntax.Scanner) (map[string]string, bool) {
	// we first need to check for the second exclamation mark
	next, ok := sc.ReadToken()
	if !ok {
		return nil, false
	}

	if next.Kind != syntax.NOT {
		logging.LogCompileError(
			sc.Context(),
			"Metadata must begin with two `!`",
			logging.LMKSyntax,
			syntax.TextPositionOfToken(next),
		)
		return nil, false
	}

	expecting := syntax.IDENTIFIER
	var currentMetaTag string
	tags := make(map[string]string)
	for {
		next, ok = sc.ReadToken()
		if !ok {
			return nil, false
		}

		// we can only exit if we are not in the middle of metadata
		if expecting == syntax.COMMA && next.Kind == syntax.NEWLINE {
			// if we have not yet decided to not compile, then we never will and
			// should return true
			return tags, true
		}

		if next.Kind == expecting {
			switch next.Kind {
			case syntax.IDENTIFIER:
				switch next.Value {
				case "nocompile":
					// nocompile automatically means we stop compilation and
					// flags don't matter since the file won't be compiled
					return nil, false
				case "arch", "os", "no_util", "unsafe", "no_warn", "warn":
					currentMetaTag = next.Value
				default:
					logging.LogCompileError(
						sc.Context(),
						fmt.Sprintf("Unknown metadata tag: `%s`", next.Value),
						logging.LMKMetadata,
						syntax.TextPositionOfToken(next),
					)
					return nil, false
				}
				// if we reach here, then we are always expected a value
				expecting = syntax.ASSIGN
			case syntax.ASSIGN:
				expecting = syntax.STRINGLIT
			case syntax.STRINGLIT:
				tagValue := strings.Trim(next.Value, "\"")

				switch currentMetaTag {
				case "arch":
					// if the architecture's don't match, we exit
					if c.targetarch != tagValue {
						return nil, false
					}
				case "os":
					// if the OS's don't match, we don't compile
					if c.targetos != tagValue {
						return nil, false
					}
				case "warn":
					// create a custom warning message
					logging.LogCompileWarning(
						sc.Context(),
						tagValue,
						logging.LMKUser,
						nil, // No actual "position" for this kind of error
					)
				default:
					// all other metadata tags just go in the tags map
					tags[currentMetaTag] = tagValue
				}

				expecting = syntax.COMMA
			case syntax.COMMA:
				expecting = syntax.IDENTIFIER
			}
		} else {
			logging.LogCompileError(
				sc.Context(),
				fmt.Sprintf("Unexpected Token: `%s`", next.Value),
				logging.LMKSyntax,
				syntax.TextPositionOfToken(next),
			)
			return nil, false
		}
	}
}
