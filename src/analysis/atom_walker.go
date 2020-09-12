package analysis

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// walkAtom walks an `atom` node and produces the corresponding HIRExpr
func (w *Walker) walkAtom(branch *syntax.ASTBranch) (common.HIRExpr, bool) {
	bElem := branch.Content[0]

	switch v := bElem.(type) {
	case *syntax.ASTBranch:
		switch v.Name {
		case "array_builder":
		case "list_builder":
		case "dict_builder":
		case "vector_builder":
		case "tupled_expr":

		}
	case *syntax.ASTLeaf:
		return w.createLiteral(v)
	}

	return nil, false
}

// createLiteral walks any literal or identifier and produces the corresponding
// HIRExpr (normally a HIRValue/HIRVar)
func (w *Walker) createLiteral(leaf *syntax.ASTLeaf) (common.HIRExpr, bool) {
	return nil, false
}
