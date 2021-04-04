package validate

import (
	"strings"
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
			unsigned := strings.Contains(atomCore.Value, "u")
			long := strings.Contains(atomCore.Value, "l")

			if unsigned && long {
				return newLiteral(atomCore, typing.PrimKindIntegral, typing.PrimIntUlong), true
			} else if unsigned {
				return w.newConstrainedLiteral(atomCore,
					primitiveTypeTable[syntax.BYTE],
					primitiveTypeTable[syntax.USHORT],
					primitiveTypeTable[syntax.UINT],
					primitiveTypeTable[syntax.ULONG],
				), true
			} else if long {
				return w.newConstrainedLiteral(atomCore,
					primitiveTypeTable[syntax.LONG],
					primitiveTypeTable[syntax.ULONG],
				), true
			} else {
				// Integer literals can be either floats or ints depending on usage
				return w.newConstrainedLiteral(atomCore, w.getCoreType("Numeric")), true
			}
		case syntax.FLOATLIT:
			return w.newConstrainedLiteral(atomCore, w.getCoreType("Floating")), true
		case syntax.NULL:
			ut := w.solver.CreateUnknown(atomCore.Position())

			return &common.HIRValue{
				ExprBase: common.NewExprBase(ut, common.RValue, true),
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

// newConstrainedLiteral creates a new literal for an undetermined primitive
// type with the given set of constraints
func (w *Walker) newConstrainedLiteral(leaf *syntax.ASTLeaf, constraints ...typing.DataType) common.HIRExpr {
	ut := w.solver.CreateUnknown(leaf.Position(), constraints...)

	return &common.HIRValue{
		ExprBase: common.NewExprBase(
			ut,
			common.RValue,
			true,
		),
		Value:    leaf.Value,
		Position: leaf.Position(),
	}
}
