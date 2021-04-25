package validate

import (
	"fmt"

	"whirlwind/common"
	"whirlwind/logging"
	"whirlwind/syntax"
	"whirlwind/typing"
)

// walkOffsetTypeList walks a data type that is offset in a larger node by some
// known amount (eg. the `type {'|' type}` in `newtype`) and has some ending
// offset (eg. `tupled_suffix`) -- this can be zero; should be number of nodes
// to be ignored on end (ie. as if it were a slice)
func (w *Walker) walkOffsetTypeList(ast *syntax.ASTBranch, startOffset, endOffset int) ([]typing.DataType, bool) {
	types := make([]typing.DataType, (ast.Len()-startOffset-endOffset)/2+1)

	for i, item := range ast.Content[startOffset : ast.Len()-endOffset] {
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

// walkTypeExt walks a type extension and returns the label
func (w *Walker) walkTypeExt(ext *syntax.ASTBranch) (typing.DataType, bool) {
	return w.walkTypeLabel(ext.BranchAt(1))
}

// primitiveTypeTable matches primitive types named by tokens to *typing.PrimType
var primitiveTypeTable = map[int]*typing.PrimitiveType{
	syntax.RUNE:    {PrimKind: typing.PrimKindText, PrimSpec: 0},
	syntax.STRING:  {PrimKind: typing.PrimKindText, PrimSpec: 1},
	syntax.F32:     {PrimKind: typing.PrimKindFloating, PrimSpec: 0},
	syntax.F64:     {PrimKind: typing.PrimKindFloating, PrimSpec: 1},
	syntax.NOTHING: {PrimKind: typing.PrimKindUnit, PrimSpec: 0},
	syntax.ANY:     {PrimKind: typing.PrimKindUnit, PrimSpec: 1},
	syntax.BOOL:    {PrimKind: typing.PrimKindBoolean, PrimSpec: 0},
	syntax.I8:      {PrimKind: typing.PrimKindIntegral, PrimSpec: typing.PrimIntI8},
	syntax.U8:      {PrimKind: typing.PrimKindIntegral, PrimSpec: typing.PrimIntU8},
	syntax.I16:     {PrimKind: typing.PrimKindIntegral, PrimSpec: typing.PrimIntI16},
	syntax.U16:     {PrimKind: typing.PrimKindIntegral, PrimSpec: typing.PrimIntU16},
	syntax.I32:     {PrimKind: typing.PrimKindIntegral, PrimSpec: typing.PrimIntI32},
	syntax.U32:     {PrimKind: typing.PrimKindIntegral, PrimSpec: typing.PrimIntU32},
	syntax.I64:     {PrimKind: typing.PrimKindIntegral, PrimSpec: typing.PrimIntI64},
	syntax.U64:     {PrimKind: typing.PrimKindIntegral, PrimSpec: typing.PrimIntU64},
}

// walkTypeLabel walks and attempts to extract a data type from a type label.
func (w *Walker) walkTypeLabel(label *syntax.ASTBranch) (typing.DataType, bool) {
	if dt, requiresRef, ok := w.walkTypeLabelCore(label.BranchAt(0), false); ok {
		// we know that since we are the exterior of walk type label, we will
		// never have the enclosing reference type required as so we can simply
		// return false.
		if requiresRef {
			w.logError(
				fmt.Sprintf("The type `%s` can only be stored by reference here", dt.Repr()),
				logging.LMKTyping,
				label.Position(),
			)

			return nil, false
		}

		return dt, true
	}

	return nil, false
}

// walkGenericTypeConstraint walks a generic type constraint node
func (w *Walker) walkGenericTypeConstraint(param *syntax.ASTBranch) ([]typing.DataType, bool) {
	typeList := make([]typing.DataType, param.Len()/2)

	for i := 2; i < param.Len(); i += 2 {
		// we have to call `walkTypeLabelCore` directly so we can allow constraints
		if dt, requiresRef, ok := w.walkTypeLabelCore(param.BranchAt(i).BranchAt(0), true); ok {
			// we know that since we are the exterior of a type label
			// (constraint), we will never have the enclosing reference type
			// required as so we can simply return false.
			if requiresRef {
				w.logError(
					fmt.Sprintf("The type `%s` can only be stored by reference here", dt.Repr()),
					logging.LMKTyping,
					param.Content[i].Position(),
				)

				return nil, false
			}

			typeList[(i-2)/2] = dt
		}
	}

	return typeList, true
}

// walkTypeLabelCore walks the inner type of the `type` node -- the type core It
// returns a boolean indicating whether or not the inner type requires a
// reference and a boolean indicating if the type was valid.  `allowConstraints`
// indicates whether type constraints should be allowed as valid types -- this
// is mostly used in generic tag walking
func (w *Walker) walkTypeLabelCore(typeCore *syntax.ASTBranch, allowConstraints bool) (typing.DataType, bool, bool) {
	var dt typing.DataType

	switch typeCore.Name {
	case "value_type":
		valueTypeCore := typeCore.BranchAt(0)
		switch valueTypeCore.Name {
		case "prim_type":
			dt = primitiveTypeTable[valueTypeCore.LeafAt(0).Kind]
		case "func_type":
			if ft, ok := w.walkFuncType(valueTypeCore); ok {
				dt = ft
			} else {
				return nil, false, false
			}
		}
	case "named_type":
		return w.walkNamedType(typeCore, allowConstraints)
	case "ref_type":
		if rt, ok := w.walkRefType(typeCore); ok {
			dt = rt
		} else {
			return nil, false, false
		}
	}

	return dt, false, true
}

// walkNamedType walks a `named_type` node fully.  It returns the type it
// extracts (or nil if no type can be determined), a flag indicating whether or
// not this named type may only be accessed by reference (eg. for self-types),
// and a flag indicating whether or not the type was able to determined/created.
func (w *Walker) walkNamedType(namedTypeLabel *syntax.ASTBranch, allowConstraints bool) (typing.DataType, bool, bool) {
	// walk the ast and extract relevant data
	var rootName, accessedName string
	var rootPos, accessedPos *logging.TextPosition
	var typeParams *syntax.ASTBranch

	for _, item := range namedTypeLabel.Content {
		switch v := item.(type) {
		case *syntax.ASTLeaf:
			if v.Kind == syntax.IDENTIFIER {
				if rootName == "" {
					rootName = v.Value
					rootPos = v.Position()
				} else {
					accessedName = v.Value
					accessedPos = v.Position()
				}
			}
		case *syntax.ASTBranch:
			if v.Name == "type_list" {
				typeParams = v
			}
		}
	}

	// look-up the core named type based on that data
	namedType, requiresRef, ok := w.lookupNamedType(rootName, accessedName, rootPos, accessedPos, allowConstraints)

	if !ok {
		return nil, false, false
	}

	// handle generics
	if typeParams != nil {
		genericPos := rootPos
		if accessedPos != nil {
			genericPos = accessedPos
		}

		if gi, ok := w.createGenericInstance(namedType, genericPos, typeParams); ok {
			namedType = gi
		} else {
			return nil, false, false
		}
	} else {
		// check if the named type is a generic with no type parameters
		switch namedType.(type) {
		case *typing.GenericType, *typing.OpaqueGenericType:
			w.logError(
				"Generic type must be provided with type parameters",
				logging.LMKGeneric,
				namedTypeLabel.Position(),
			)

			return nil, false, false
		}
	}

	return namedType, requiresRef, true
}

// lookupNamedType performs the full look-up of a named type based on the values
// that are extracted during the execution of walkNamedType.  It handles opaque
// types and self-types.  Generics are processed by walkNamedType.  It returns
// the same parameters as walkNamedType.
func (w *Walker) lookupNamedType(rootName, accessedName string, rootPos, accessedPos *logging.TextPosition, allowConstraints bool) (typing.DataType, bool, bool) {
	// NOTE: we don't consider algebraic variants here (statically accessed)
	// because they cannot be used as types.  They are only values so despite
	// using the `::` syntax, they are simply not usable here (and simply saying
	// no package exists is good enough).  This is also why we don't need to
	// consider opaque algebraic variants since such accessing should never
	// occur.

	// if there is no accessed name, than this just a standard symbol access
	if accessedName == "" {
		symbol, ok := w.globalLookup(rootName)

		// check for generic type parameters (in the generic ctx)
		if !ok {
			// there will almost never be more than one type parameter so we
			// don't really need to use a map here and keeping it as a list
			// makes converting the `genericCtx` into a generic easier
			for _, typeParam := range w.genericCtx {
				if typeParam.Name == rootName {
					return typeParam, false, true
				}
			}

			// also make sure to check `interfGenericCtx` (by same logic as
			// regular `genericCtx`)
			for _, typeParam := range w.interfGenericCtx {
				if typeParam.Name == rootName {
					return typeParam, false, true
				}
			}
		} else {
			// the symbol exists in the regular local table
			if symbol.DefKind != common.DefKindTypeDef || (allowConstraints && symbol.DefKind == common.DefKindConstraint) {
				w.logError(
					fmt.Sprintf("Symbol `%s` is not a type", symbol.Name),
					logging.LMKUsage,
					rootPos,
				)

				return nil, false, false
			}

			if w.declStatus == common.DSExported {
				if symbol.VisibleExternally() {
					return symbol.Type, false, true
				}

				w.logError(
					fmt.Sprintf("Symbol `%s` must be exported to be used in an exported definition", symbol.Name),
					logging.LMKUsage,
					rootPos,
				)

				return nil, false, false
			}

			return symbol.Type, false, true
		}

		// only reach here if were unable to resolve at all
		if w.resolving {
			// if we are resolving, then we need to check for opaque types and
			// self types (checked in two separate if blocks)
			if os, ok := w.sharedOpaqueSymbolTable.LookupOpaque(w.SrcPackage.PackageID, rootName); ok {
				return os.Type, w.opaqueSymbolRequiresRef(os), true
			} else if w.selfType != nil && rootName == w.currentDefName {
				w.selfTypeUsed = true
				return w.selfType, w.selfTypeRequiresRef, true
			}
		} else {
			// just a regular undefined symbol
			w.LogUndefined(rootName, rootPos)
			return nil, false, false
		}
	} else if w.declStatus == common.DSExported {
		w.logError(
			"Unable to use implicitly imported symbol in exported definition",
			logging.LMKUsage,
			accessedPos,
		)
	}

	if pkg, ok := w.SrcFile.VisiblePackages[rootName]; ok {
		if symbol, ok := w.implicitImport(pkg, accessedName); ok {
			if symbol.DefKind != common.DefKindTypeDef || (allowConstraints && symbol.DefKind == common.DefKindConstraint) {
				w.logError(
					fmt.Sprintf("Symbol `%s` is not a type", symbol.Name),
					logging.LMKUsage,
					accessedPos,
				)

				return nil, false, false
			}

			return symbol.Type, false, true
		} else if w.resolving {
			// opaque symbols may exist in the other package if we are still
			// resolving (can be accessed via an implicit import)
			if os, ok := w.sharedOpaqueSymbolTable.LookupOpaque(pkg.PackageID, accessedName); ok {
				return os.Type, w.opaqueSymbolRequiresRef(os), true
			}
		}

		w.LogNotVisibleInPackage(accessedName, rootName, accessedPos)
		return nil, false, false
	}

	w.logError(
		fmt.Sprintf("Package `%s` is not defined", rootName),
		logging.LMKName,
		rootPos,
	)
	return nil, false, false
}

// opaqueSymbolRequiresRef checks within the given context whether or not the
// shared opaque symbol must be accessed by reference.  This function assumes
// that the opaque symbol can be used
func (w *Walker) opaqueSymbolRequiresRef(os *common.OpaqueSymbol) bool {
	if os.RequiresRef {
		if pkgIDs, ok := os.DependsOn[w.currentDefName]; ok {
			for _, pkgID := range pkgIDs {
				if pkgID == w.SrcPackage.PackageID {
					return true
				}
			}
		}
	}

	return false
}

// walkRefType walks a `ref_type` node and attempts to produce a reference type
func (w *Walker) walkRefType(branch *syntax.ASTBranch) (typing.DataType, bool) {
	elemtype, _, ok := w.walkTypeLabelCore(branch.LastBranch(), false)

	if !ok {
		return nil, false
	}

	// length 3 => constant (`&const type`)
	if branch.Len() == 3 {
		return &typing.RefType{
			ElemType: elemtype,
			Constant: true,
		}, true
	} else {
		return &typing.RefType{
			ElemType: elemtype,
		}, true
	}
}

// walkFuncType walks a function type label
func (w *Walker) walkFuncType(branch *syntax.ASTBranch) (typing.DataType, bool) {
	ft := &typing.FuncType{
		Async:   branch.LeafAt(0).Kind == syntax.ASYNC,
		Boxable: true,
		Boxed:   true,
	}

	for _, item := range branch.Content {
		if itembranch, ok := item.(*syntax.ASTBranch); ok {
			// only two options are `func_type_args` and `type_list`
			if itembranch.Name == "func_type_args" {
				if !w.walkRecursiveRepeat(itembranch.Content, func(argbranch *syntax.ASTBranch) bool {
					// both branches can be walked the same way
					if adt, ok := w.walkTypeLabel(argbranch.LastBranch()); ok {
						ft.Args = append(ft.Args, &typing.FuncArg{
							Val: &typing.TypedValue{
								Type: adt,
							},
							// has to be a regular arg to be optional and have a
							// length of 2 (b/c `~type` is for optional, `type`
							// is for regular)
							Optional:   argbranch.Name == "func_type_arg" && argbranch.Len() == 2,
							Indefinite: argbranch.Name == "func_type_var_arg",
						})

						return true
					}

					return false
				}) {
					return nil, false
				}
			} else if typeList, ok := w.walkTypeList(itembranch); ok {
				// just a single return value
				if len(typeList) == 1 {
					ft.ReturnType = typeList[0]
				} else /* tuple of return values */ {
					ft.ReturnType = (typing.TupleType)(typeList)
				}
			} else {
				return nil, false
			}
		}
	}

	return ft, true
}
