package analysis

import (
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// walkSimpleStmt walks a `simple_stmt` node
func (w *Walker) walkSimpleStmt(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	stmtBranch := branch.BranchAt(0)

	switch stmtBranch.Name {
	case "variable_decl":
		return w.walkVarDecl(stmtBranch)
	case "expr_stmt":
		return w.walkExprStmt(stmtBranch)
	case "continue_stmt":
		if w.CurrScope().Context.EnclosingLoop {
			return &common.HIRSimpleStmt{
				StmtKind: common.SSKContinue,
			}, true
		}

		util.ThrowError(
			"Unable to use `continue` outside of a loop",
			"Context",
			stmtBranch.Position(),
		)

		return nil, false
	case "break_stmt":
		if w.CurrScope().Context.EnclosingLoop {
			return &common.HIRSimpleStmt{
				StmtKind: common.SSKBreak,
			}, true
		}

		util.ThrowError(
			"Unable to use `break` outside of a loop",
			"Context",
			stmtBranch.Position(),
		)

		return nil, false
	case "fallthrough_stmt":
		var ssk int
		if stmtBranch.Len() == 1 {
			ssk = common.SSKFallthrough
		} else {
			ssk = common.SSKFallMatch
		}

		if w.CurrScope().Context.EnclosingMatch {
			return &common.HIRSimpleStmt{
				StmtKind: ssk,
			}, true
		}

		util.ThrowError(
			"Unable to use `fallthrough` outside of a match statement",
			"Context",
			stmtBranch.Position(),
		)

		return nil, false
	// Context.Func should always be valid (for yield and return)
	case "return_stmt":
		if stmtBranch.Len() == 1 {
			if types.CoerceTo(nothingType, w.CurrScope().Context.Func.ReturnType) {
				return &common.HIRSimpleStmt{
					StmtKind: common.SSKReturn,
				}, true
			}

			util.ThrowError(
				"Unable to return nothing from a function that expects a value",
				"Type",
				stmtBranch.Position(),
			)

			return nil, false
		}

		return w.makeReturnValueStmt(stmtBranch, common.SSKReturn)
	case "yield_stmt":
		return w.makeReturnValueStmt(stmtBranch, common.SSKYield)
	}

	return nil, false
}

// walkVarDecl walks a `variable_decl` node
func (w *Walker) walkVarDecl(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	return nil, false
}

// walkExprStmt walks an `expr_stmt` node
func (w *Walker) walkExprStmt(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	return nil, false
}

// makeReturnValueStmt makes a return-like (`return` or `yield`) that returns a
// value or values (ie. no empty returns)
func (w *Walker) makeReturnValueStmt(stmtBranch *syntax.ASTBranch, sskind int) (common.HIRNode, bool) {
	if exprList, ok := w.walkExprList(stmtBranch.BranchAt(1)); ok {
		for i, expr := range exprList {
			if err := checkReturn(expr); err != nil {
				util.ThrowError(
					err.Error(),
					"Value",
					stmtBranch.BranchAt(1).BranchAt(i*2).Position(),
				)

				return nil, false
			}
		}

		var dt types.DataType
		if len(exprList) == 1 {
			dt = exprList[0].Type()
		} else {
			dt = types.TupleType(typesFromExprList(exprList))
		}

		if types.CoerceTo(dt, w.CurrScope().Context.Func.ReturnType) {
			w.CurrScope().ReturnsValue = true

			return &common.HIRSimpleStmt{
				StmtKind: common.SSKReturn,
				Content:  exprList,
			}, true
		}

		ThrowCoercionError(
			dt,
			w.CurrScope().Context.Func.ReturnType,
			stmtBranch.Content[1].Position(),
		)

		return nil, false
	}

	return nil, false
}
