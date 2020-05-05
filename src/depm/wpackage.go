package depm

import "github.com/ComedicChimera/whirlwind/src/syntax"

type WhirlPackage struct {
	files map[string]*syntax.ASTBranch
}
