package validate

import (
	"whirlwind/common"
	"whirlwind/syntax"
	"whirlwind/typing"
)

// walkAtom walks an `atom` node.  Basically handles all bottom-level "values"
func (w *Walker) walkAtom(branch *syntax.ASTBranch) (common.HIRExpr, bool) {
	switch atomCore := branch.Content[0].(type) {
	case *syntax.ASTBranch:

	case *syntax.ASTLeaf:
		switch atomCore.Kind {
		case syntax.STRINGLIT:
			return newLiteral(atomCore, typing.PrimKindText, 1), true
		case syntax.RUNELIT:
			return newLiteral(atomCore, typing.PrimKindText, 0), true
		case syntax.BOOLLIT:
			return newLiteral(atomCore, typing.PrimKindBoolean, 0), true
		case syntax.IDENTIFIER:
			if sym, ok := w.localLookup(atomCore.Value); ok {
				return common.NewIdentifierFromSymbol(sym, atomCore.Position()), true
			} else {
				w.LogUndefined(atomCore.Value, atomCore.Position())
			}
		case syntax.INTLIT:
			return w.newUndeterminedLiteral(atomCore, "Integral"), true
		case syntax.FLOATLIT:
			return w.newUndeterminedLiteral(atomCore, "Floating"), true
		case syntax.NULL:
			tu := w.solver.CreateUnknown(atomCore.Position())

			return &common.HIRValue{
				ExprBase: common.NewExprBase(tu, common.RValue, true),
				Value:    atomCore.Value,
				Position: atomCore.Position(),
			}, true
		}
	}

	return nil, false
}

// newLiteral creates a new literal primitive from the given branch
func newLiteral(leaf *syntax.ASTLeaf, primKind, primSpec uint8) common.HIRExpr {
	return &common.HIRValue{
		ExprBase: common.NewExprBase(
			&typing.PrimitiveType{
				PrimKind: primKind,
				PrimSpec: primSpec,
			},
			common.RValue,
			true,
		),
		Value:    leaf.Value,
		Position: leaf.Position(),
	}
}

// newUndeterminedLiteral creates a new literal for an undetermined primitive
// type (eg. int literals, float literals, etc) since these types can assume one
// of many more specific types depending on usage.  It takes the name of the
// appropriate constaint interface
func (w *Walker) newUndeterminedLiteral(leaf *syntax.ASTLeaf, constraintName string) common.HIRExpr {
	tu := w.solver.CreateUnknown(leaf.Position(), w.getCoreType(constraintName))

	return &common.HIRValue{
		ExprBase: common.NewExprBase(
			tu,
			common.RValue,
			true,
		),
		Value:    leaf.Value,
		Position: leaf.Position(),
	}
}
