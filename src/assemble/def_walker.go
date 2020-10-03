package assemble

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/logging"
	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// walkDef walks the AST of any given definition and attempts to determine its
// corresponding symbols, its HIR node, and any unknowns this definition has. If
// this definition has unknowns, then a nil HIR node is returned and a non-nil
// map of unknowns. Otherwise, a non-nil HIR node is returned and a nil map of
// unknowns.  This is the main definition analysis function and accepts a
// `definition` node.
func (r *Resolver) walkDef(dast *syntax.ASTBranch) (map[*common.Symbol]*logging.TextPosition, common.HIRNode, map[string]*logging.TextPosition) {
	return nil, nil, nil
}
