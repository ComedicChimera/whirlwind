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
		}

		return w.makeReturnValueStmt(stmtBranch, common.SSKReturn)
	case "yield_stmt":
		return w.makeReturnValueStmt(stmtBranch, common.SSKYield)
	case "delete_stmt":
		if exprs, ok := w.walkExprList(stmtBranch.BranchAt(1)); ok {
			var exprsValid bool

			for i, expr := range exprs {
				if rt, ok := expr.Type().(*types.ReferenceType); ok {
					if rt.Owned && rt.Nullable {
						continue
					}

					util.ThrowError(
						"Only owned, nullable references can be deleted",
						"Type",
						stmtBranch.BranchAt(1).Content[i*2].Position(),
					)
				} else {
					util.ThrowError(
						"Only references can be deleted",
						"Type",
						stmtBranch.BranchAt(1).Content[i*2].Position(),
					)
				}

				exprsValid = false
			}

			if exprsValid {
				return &common.HIRSimpleStmt{
					StmtKind: common.SSKDelete,
					Content:  exprs,
				}, true
			}
		}
	case "resize_stmt":
	}

	return nil, false
}

// walkVarDecl walks a `variable_decl` node
func (w *Walker) walkVarDecl(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	var constant, volatile bool
	varDecl := &common.HIRVarDecl{
		Vars: make(map[string]*common.DeclVar),
	}

	for _, item := range branch.Content {
		switch v := item.(type) {
		case *syntax.ASTLeaf:
			switch v.Kind {
			case syntax.CONST:
				constant = true
			case syntax.VOL:
				volatile = true
			}
		case *syntax.ASTBranch:
			switch v.Name {
			case "var":
				sym := &common.Symbol{
					Constant:   constant,
					Name:       v.LeafAt(0).Value,
					DeclStatus: w.DeclStatus,
					DefKind:    common.SKindNamedValue,
				}

				var initializer common.HIRExpr

				switch v.Len() {
				case 1:
					util.ThrowError(
						"Unable to determine type of variable",
						"Type",
						v.Position(),
					)

					return nil, false
				case 2:
					switch v.Name {
					case "type_ext":
						if dt, ok := w.walkTypeExt(v.BranchAt(1)); ok {
							sym.Type = dt
						} else {
							return nil, false
						}
					case "initializer":
						if expr, ok := w.walkExpr(v.BranchAt(1).BranchAt(1)); ok {
							sym.Type = expr.Type()
							initializer = expr
						} else {
							return nil, false
						}
					}
				case 3:
					if dt, ok := w.walkTypeExt(v.BranchAt(1)); ok {
						sym.Type = dt
					} else {
						return nil, false
					}

					if expr, ok := w.walkExpr(v.BranchAt(2).BranchAt(1)); ok {
						if !types.CoerceTo(expr.Type(), sym.Type) {
							ThrowCoercionError(
								expr.Type(), sym.Type,
								v.BranchAt(2).Content[1].Position(),
							)

							return nil, false
						}

						initializer = expr
					} else {
						return nil, false
					}
				}

				varDecl.Vars[sym.Name] = &common.DeclVar{
					Sym: sym, Initializer: initializer, Volatile: volatile,
				}

				if !w.Define(sym) {
					ThrowMultiDefError(sym.Name, v.Content[0].Position())
					return nil, false
				}
			case "unpack_var":
				endingBranch := v.Last().(*syntax.ASTBranch).BranchAt(1)

				if endingBranch.Name == "initializer" {
					if initExpr, ok := w.walkExpr(v.Last().(*syntax.ASTBranch).BranchAt(1)); ok {
						if tt, ok := initExpr.Type().(*types.TupleType); ok {
							if exVars, ok := w.walkTupleUnpackVar(v, tt); ok {
								for name, uvar := range exVars {
									sym := &common.Symbol{
										Name:       name,
										Type:       uvar.Type,
										Constant:   constant,
										DeclStatus: w.DeclStatus,
										DefKind:    common.SKindNamedValue,
									}

									if !w.Define(sym) {
										ThrowMultiDefError(name, uvar.Pos)
										return nil, false
									}

									varDecl.Vars[name] = &common.DeclVar{
										Sym: sym, Volatile: volatile,
									}
								}

								varDecl.TupleInit = initExpr
							} else {
								return nil, false
							}
						} else {
							util.ThrowError(
								"Expecting a tuple on the rhs of the declaration",
								"Type",
								v.Last().Position(),
							)

							return nil, false
						}
					} else {
						return nil, false
					}
				} else if dt, ok := w.walkTypeLabel(endingBranch); ok /* `type_ext` */ {
					for _, item := range v.Content {
						switch v2 := item.(type) {
						case *syntax.ASTBranch:
							if idLeaf, ok := v2.Content[0].(*syntax.ASTLeaf); ok {
								sym := &common.Symbol{
									Name:       idLeaf.Value,
									Type:       dt,
									Constant:   constant,
									DeclStatus: w.DeclStatus,
									DefKind:    common.SKindNamedValue,
								}

								if !w.Define(sym) {
									ThrowMultiDefError(sym.Name, idLeaf.Position())
									return nil, false
								}

								varDecl.Vars[idLeaf.Value] = &common.DeclVar{
									Sym:      sym,
									Volatile: volatile,
								}
							} else {
								util.ThrowError(
									"All variables in a multideclaration should be on the same level",
									"Usage",
									v2.Position(),
								)

								return nil, false
							}
						}
					}
				}
			}
		}
	}

	return varDecl, true
}

// walkExprStmt walks an `expr_stmt` node
func (w *Walker) walkExprStmt(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	return nil, false
}

// makeReturnValueStmt makes a return-like (`return` or `yield`) that returns a
// value or values (ie. no empty returns)
func (w *Walker) makeReturnValueStmt(stmtBranch *syntax.ASTBranch, sskind int) (common.HIRNode, bool) {
	if exprList, ok := w.walkExprList(stmtBranch.BranchAt(1)); ok {
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