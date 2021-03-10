package build

import (
	"fmt"
	"strings"

	"whirlwind/logging"
	"whirlwind/syntax"
)

// preprocessFile reads the first few tokens of a file to extract any compiler
// metadata and decide whether or not to compile the file
func (c *Compiler) preprocessFile(sc *syntax.Scanner) (bool, map[string]string, error) {
	next, err := sc.ReadToken()
	if err != nil {
		return false, nil, err
	}

	// if we encountered metadata
	if next.Kind == syntax.NOT {
		return c.readMetadata(sc)
	} else {
		// if the scanner has not populated to lookahead, we can just
		// store the token we read in there
		sc.UnreadToken(next)
	}

	return true, nil, nil
}

// readMetadata reads and processes metadata at the start of a file
func (c *Compiler) readMetadata(sc *syntax.Scanner) (bool, map[string]string, error) {
	// we first need to check for the second exclamation mark
	next, err := sc.ReadToken()
	if err != nil {
		return false, nil, err
	}

	if next.Kind != syntax.NOT {
		return false, nil, &logging.LogMessage{
			Message:  "Metadata must begin with two `!`",
			Kind:     logging.LMKSyntax,
			Position: syntax.TextPositionOfToken(next),
			Context:  sc.Context(),
		}
	}

	expecting := syntax.IDENTIFIER
	var currentMetaTag string
	tags := make(map[string]string)
	for true {
		next, err = sc.ReadToken()
		if err != nil {
			return false, nil, err
		}

		// we can only exit if we are not in the middle of metadata
		if expecting == syntax.COMMA && next.Kind == syntax.NEWLINE {
			// if we have not yet decided to not compile, then we never will and
			// should return true
			return true, tags, nil
		}

		if next.Kind == expecting {
			switch next.Kind {
			case syntax.IDENTIFIER:
				switch next.Value {
				case "nocompile":
					// nocompile automatically means we stop compilation and
					// flags don't matter since the file won't be compiled
					return false, nil, nil
				case "arch", "os", "no_util", "unsafe_all", "no_warn_all", "warn":
					currentMetaTag = next.Value
				default:
					return false, nil, &logging.LogMessage{
						Message:  fmt.Sprintf("Unknown metadata tag: `%s`", next.Value),
						Kind:     logging.LMKMetadata,
						Position: syntax.TextPositionOfToken(next),
						Context:  sc.Context(),
					}
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
						return false, nil, nil
					}
				case "os":
					// if the OS's don't match, we don't compile
					if c.targetos != tagValue {
						return false, nil, nil
					}
				case "warn":
					// create a custom warning message
					logging.LogWarning(
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
			return false, nil, &logging.LogMessage{
				Message:  fmt.Sprintf("Unexpected Token: `%s`", next.Value),
				Kind:     logging.LMKSyntax,
				Position: syntax.TextPositionOfToken(next),
				Context:  sc.Context(),
			}
		}
	}

	// unreachable
	return false, nil, nil
}
