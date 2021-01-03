package validate

import (
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/typing"
)

// walkOffsetTypeList walks a data type that is offset in a larger node by some
// known amount (eg. the `type {'|' type}` in `newtype`)
func (w *Walker) walkOffsetTypeList(ast *syntax.ASTBranch, offset int) ([]typing.DataType, bool) {
	types := make([]typing.DataType, (ast.Len()-offset)/2+1)

	for i, item := range ast.Content[offset:] {
		if i%2 == 0 {
			if dt, ok := w.walkTypeLabel(item.(*syntax.ASTBranch)); ok {
				types[i/2] = dt
			} else {
				return nil, false
			}
		}
	}

	return types, true
}

// walkTypeList walks a `type_list` node (or any node that is composed of data
// types that are evenly spaced, ie. of the following form `type {TOKEN type}`)
func (w *Walker) walkTypeList(ast *syntax.ASTBranch) ([]typing.DataType, bool) {
	types := make([]typing.DataType, ast.Len()/2+1)

	for i, item := range ast.Content {
		if i%2 == 0 {
			if dt, ok := w.walkTypeLabel(item.(*syntax.ASTBranch)); ok {
				types[i/2] = dt
			} else {
				return nil, false
			}
		}
	}

	return types, true
}

// walkTypeLabel walks and attempts to extract a data type from a type label. If
// this function fails, it does NOT set `w.fatalDefError`.
func (w *Walker) walkTypeLabel(label *syntax.ASTBranch) (typing.DataType, bool) {
	return nil, false
}
