package validate

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// WalkDef walks the AST of any given definition and attempts to determine if it
// can be defined.  If it can, it returns the produced HIR node and the value
// `true`. If it cannot, then it determines first if the only errors are due to
// missing symbols, in which case it returns a map of unknowns and `true`.
// Otherwise, it returns `false`. All unmentioned values for each case will be
// nil. This is the main definition analysis function and accepts a `definition`
// node.  This function is mainly intended to work with the Resolver and
// PackageAssembler.
func (w *Walker) WalkDef(dast *syntax.ASTBranch) (common.HIRNode, map[string]*UnknownSymbol, bool) {

	// collect and clear our unknowns after they have collected for the definition
	unknowns := w.Unknowns
	w.clearUnknowns()
	return nil, unknowns, true
}
