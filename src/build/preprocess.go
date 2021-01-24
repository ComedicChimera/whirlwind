package build

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/logging"
	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// preprocessFile reads the first few tokens of a file to extract any compiler
// metadata and decide whether or not to compile the file
func (c *Compiler) preprocessFile(sc *syntax.Scanner) (bool, error) {
	next, err := sc.ReadToken()
	if err != nil {
		return false, err
	}

	// if we encountered metadata
	if next.Kind == syntax.NOT {
		return c.readMetadata(sc)
	} else {
		// if the scanner has not populated to lookahead, we can just
		// store the token we read in there
		sc.UnreadToken(next)
	}

	return true, nil
}

// readMetadata reads and processes metadata at the start of a file
func (c *Compiler) readMetadata(sc *syntax.Scanner) (bool, error) {
	// we first need to check for the second exclamation mark
	next, err := sc.ReadToken()
	if err != nil {
		return false, err
	}

	if next.Kind != syntax.NOT {
		return false, &logging.LogMessage{
			Message:  "Metadata must begin with two `!`",
			Kind:     logging.LMKSyntax,
			Position: syntax.TextPositionOfToken(next),
			Context:  sc.Context(),
		}
	}

	expecting := syntax.IDENTIFIER
	var currentMetaTag string
	for true {
		next, err = sc.ReadToken()
		if err != nil {
			return false, err
		}

		// we can only exit if we are not in the middle of metadata
		if expecting == syntax.COMMA && next.Kind == syntax.NEWLINE {
			// if we have not yet decided to not compile, then we never will and
			// should return true
			return true, nil
		}

		if next.Kind == expecting {
			switch next.Kind {
			case syntax.IDENTIFIER:
				switch next.Value {
				case "nocompile":
					// nocompile automatically means we stop compilation
					return false, nil
				case "arch", "os":
					currentMetaTag = next.Value
				default:
					return false, &logging.LogMessage{
						Message:  fmt.Sprintf("Unknown metadata tag: `%s`", next.Value),
						Kind:     logging.LMKUsage,
						Position: syntax.TextPositionOfToken(next),
						Context:  sc.Context(),
					}
				}
				// if we reach here, then we are always expected a value
				expecting = syntax.ASSIGN
			case syntax.ASSIGN:
				expecting = syntax.STRINGLIT
			case syntax.STRINGLIT:
				if currentMetaTag == "arch" {
					// if the architecture's don't match, we exit
					if c.targetarch != next.Value {
						return false, nil
					}
				} else {
					// only other option is os -- if they don't match, we don't
					// compile
					if c.targetos != next.Value {
						return false, nil
					}
				}

				expecting = syntax.COMMA
			case syntax.COMMA:
				expecting = syntax.IDENTIFIER
			}
		} else {
			return false, &logging.LogMessage{
				Message:  fmt.Sprintf("Unexpected Token: `%s`", next.Value),
				Kind:     logging.LMKSyntax,
				Position: syntax.TextPositionOfToken(next),
				Context:  sc.Context(),
			}
		}
	}

	// unreachable
	return false, nil
}
