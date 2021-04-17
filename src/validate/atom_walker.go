package validate

import (
	"fmt"
	"strings"
	"whirlwind/common"
	"whirlwind/logging"
	"whirlwind/syntax"
	"whirlwind/typing"
)

// walkAtomExpr walks an `atom_expr` node.  Handles function calls, generic
// initializations, struct initializers, and field/method accesses.
func (w *Walker) walkAtomExpr(branch *syntax.ASTBranch) (common.HIRExpr, bool) {
	var result common.HIRExpr

	// TODO: static-get for package accesses
	for _, item := range branch.Content {
		itembranch := item.(*syntax.ASTBranch)
		switch itembranch.Name {
		case "atom":
			if atom, ok := w.walkAtom(itembranch); ok {
				result = atom
			} else {
				return nil, false
			}
		case "trailer":
			if trailer, ok := w.walkTrailer(result, itembranch); ok {
				result = trailer
			} else {
				return nil, false
			}
		case "make_expr":
			// TODO: figure out how make exprs should work (only for
			// collections?, all r-value references?...)
		}
	}

	return result, true
}

// walkTrailer walks a `trailer` node an `atom_expr`
func (w *Walker) walkTrailer(root common.HIRExpr, branch *syntax.ASTBranch) (common.HIRExpr, bool) {
	opLeaf := branch.LeafAt(0)
	rootType, isKnown := innerType(root.Type())
	if !isKnown {
		w.logError(
			fmt.Sprintf("Unable to use `%s` operator on an undetermined type", opLeaf.Value),
			logging.LMKTyping,
			opLeaf.Position(),
		)

		return nil, false
	}

	switch opLeaf.Kind {
	case syntax.DOT:
		if dt, ok := w.getFieldOrMethod(rootType, branch.LeafAt(1).Value, opLeaf.Position(), branch.Content[1].Position()); ok {
			return &common.HIRDotAccess{
				RootType:  rootType,
				FieldType: dt,
				FieldName: branch.LeafAt(1).Value,
			}, true
		}
	case syntax.LPAREN:
	case syntax.LBRACE:
	case syntax.LBRACKET:
	case syntax.GETNAME:
	}

	return nil, false
}

// getFieldOrMethod attempts to access a named field or method of a type.  This
// function implements the logic for the `.` operator.  It assumes the type
// passed in is already an inner type.
func (w *Walker) getFieldOrMethod(dt typing.DataType, fieldName string, opPos, namePos *logging.TextPosition) (typing.DataType, bool) {
	// unwrap reference types to their element types -- the `.` operator always
	// translates as a reference operator; not possible for a reference to have
	// fields or methods.  This also means that we have to check again for
	// unknown types since they can't be used as operands for the `.` operator
	// even if they are element types of a reference.
	if rt, ok := dt.(*typing.RefType); ok {
		if elemtype, ok := innerType(rt.ElemType); ok {
			dt = elemtype
		} else {
			w.logError(
				"Unable to use `.` operator on an undetermined type",
				logging.LMKTyping,
				opPos,
			)

			return nil, false
		}
	}

	// TODO: handle @introspect or remove as annotation?

	// handle all the type specific forms of this operator first; this switch
	// has to happen after the check for reference types so that `&struct` and
	// `&interf` get handled properly
	switch v := dt.(type) {
	case *typing.StructType:
		if field, ok := v.Fields[fieldName]; ok {
			return field.Type, true
		}
	case *typing.InterfType:
		if method, ok := v.Methods[fieldName]; ok {
			return method.Signature, true
		}
	}

	// now we check for all bound methods
	for _, binding := range w.getBindings(dt) {
		if method, ok := binding.Methods[fieldName]; ok {
			return method.Signature, true
		}
	}

	// if we reach here, then no match was found
	w.logError(
		fmt.Sprintf("Type `%s` has no bound method `%s`", dt.Repr(), fieldName),
		logging.LMKProp,
		namePos,
	)

	return nil, false
}

// walkFuncCall walks a `trailer` for a function call -- checking and generating
// a function call expression based on it
func (w *Walker) walkFuncCall(root common.HIRExpr, rootInnerType typing.DataType, branch *syntax.ASTBranch) (common.HIRExpr, bool) {
	var fntype *typing.FuncType
	switch v := rootInnerType.(type) {
	case *typing.FuncType:
		fntype = v
	case *typing.GenericType:
		if gnode, ok := w.createImplicitGenericInstance(v, root, branch.Position()); ok {
			if gfntype, ok := gnode.Type().(*typing.FuncType); ok {
				fntype = gfntype
				root = gnode
			}
		} else {
			// ah yes, the classic `fallthrough` not allowed in type switches...
			w.logError(
				fmt.Sprintf("Unable to call non-function of type `%s`", rootInnerType.Repr()),
				logging.LMKTyping,
				branch.Position(),
			)

			return nil, false
		}
	default:
		w.logError(
			fmt.Sprintf("Unable to call non-function of type `%s`", rootInnerType.Repr()),
			logging.LMKTyping,
			branch.Position(),
		)

		return nil, false
	}

	argDts := make(map[string]typing.DataType)
	argNodes := make(map[string]common.HIRExpr)

	// length of branch = 3 => there are arguments
	if branch.Len() == 3 {
		var namedArgumentsEncountered bool
		argPos := 0
		for _, item := range branch.BranchAt(1).Content {
			if arg, ok := item.(*syntax.ASTBranch); ok {
				// positional argument
				if arg.Len() == 1 {
					if namedArgumentsEncountered {
						w.logError(
							"All positional arguments must come before named arguments",
							logging.LMKArg,
							arg.Position(),
						)

						return nil, false
					}

					if argPos >= len(fntype.Args) {
						w.logError(
							fmt.Sprintf("Function expects at most `%d` arguments, but received `%d`", len(fntype.Args), argPos+1),
							logging.LMKArg,
							arg.Position(),
						)
					}

					farg := fntype.Args[argPos]
					if argexpr, ok := w.walkExpr(arg.BranchAt(0)); ok {
						// TODO: argument expression type checking and handling
						// of indefinite arguments
						_ = argexpr
					} else {
						return nil, false
					}

					if !farg.Indefinite {
						argPos++
					}
				} else /* named argument */ {
					namedArgumentsEncountered = true
					// TODO: named arguments
				}
			}
		}
	}

	for _, farg := range fntype.Args {
		if !farg.Optional && !farg.Indefinite {
			// TODO: handle "unnamed" arguments (generated from first-class
			// function types)
			if _, ok := argDts[farg.Name]; !ok {
				w.logError(
					fmt.Sprintf("No value specified for required argument: `%s`", farg.Name),
					logging.LMKArg,
					branch.Position(),
				)

				return nil, false
			}
		}
	}

	rt := w.solver.DeduceApp(fntype, argDts)
	return &common.HIRApp{
		ExprBase:  common.NewExprBase(rt, common.RValue, false),
		Func:      root,
		Arguments: argNodes,
	}, true
}

// createImplicitGenericInstance creates a generic instance with all of the type
// parameters filled out as unknown types.  It returns the expression node
// representing the creation of the generic instance (whose return value is the
// new generic instance)
func (w *Walker) createImplicitGenericInstance(gt *typing.GenericType, root common.HIRExpr, opPos *logging.TextPosition) (common.HIRExpr, bool) {
	// TODO -- notes:
	// - how to give meaningful error messages?
	// - create generic nstance without calling CoerceTo on unknowns?
	// - deal with instance collisions -- unknown instance that ends up being
	//   the same as other instances?
	// - make sure unknowns share the constraints of their type parameters

	return nil, false
}

// getIdentifierFromExpr reinterprets an `expr` node as a literal identifier.
// This function is meant to be used in contexts where an identifier is the
// actual only accepted token, but due to the limitations of the parser a full
// `expr` node had to be generated.  It is called recursively for all
// sub-expressions of the `expr` until a root node is reached.
func (w *Walker) getIdentifierFromExpr(expr *syntax.ASTBranch) (*syntax.ASTLeaf, bool) {
	// if the `expr` has more than one element, then it cannot be a pure
	// identifier node and any extra items are invalid is invalid
	if expr.Len() > 1 {
		w.logError(
			"Expecting an identifier not an expression",
			logging.LMKSyntax,
			expr.Position(),
		)

		return nil, false
	}

	switch v := expr.Content[0].(type) {
	case *syntax.ASTBranch:
		// just recur down the tree
		return w.getIdentifierFromExpr(v)
	case *syntax.ASTLeaf:
		// we are at the root and need to extract the identifier if it exists
		if v.Kind == syntax.IDENTIFIER {
			return v, true
		} else {
			w.logError(
				fmt.Sprintf("Expecting an identifer not `%s`", v.Value),
				logging.LMKSyntax,
				expr.Position(),
			)

			return nil, false
		}
	}

	// unreachable
	return nil, false
}

// walkAtom walks an `atom` node.  Basically handles all bottom-level "values"
func (w *Walker) walkAtom(branch *syntax.ASTBranch) (common.HIRExpr, bool) {
	switch atomCore := branch.Content[0].(type) {
	case *syntax.ASTBranch:
		switch atomCore.Name {
		case "tupled_expr":
			if exprs, ok := w.walkExprList(atomCore.BranchAt(1)); ok {
				// length 1 => subexpression
				if len(exprs) == 1 {
					return exprs[0], true
				}

				return &common.HIRSequence{
					ExprBase: common.NewExprBase(
						typing.TupleType(typeListFromExprs(exprs)),
						common.LValue,
						false,
					),
					Values: exprs,
				}, true
			}

			return nil, false
		}
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
