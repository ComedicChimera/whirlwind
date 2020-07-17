package depm

import (
	"strings"

	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// preprocessFile checks for and walks annotations at the top of an already
// parsed file.  It returns a map of the annotations it collects.
func preprocessFile(fileAST *syntax.ASTBranch) map[string]string {
	annotations := make(map[string]string)

loop:
	for _, node := range fileAST.Content {
		branch := node.(*syntax.ASTBranch)

		switch branch.Name {
		case "annotation":
			if len(branch.Content) == 2 {
				annotations[branch.Content[1].(*syntax.ASTLeaf).Value] = ""
			} else {
				annotations[branch.Content[1].(*syntax.ASTLeaf).Value] =
					strings.Trim(branch.Content[2].(*syntax.ASTLeaf).Value, "\"")
			}
		case "exported_import", "import_stmt", "top_level":
			break loop
		}
	}

	return annotations
}
