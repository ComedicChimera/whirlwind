package validate

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/logging"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/typing"
)

// walkOffsetTypeList walks a data type that is offset in a larger node by some
// known amount (eg. the `type {'|' type}` in `newtype`) and has some ending
// offset (eg. `tupled_suffix`) -- this can be zero; should be number of nodes
// to be ignored on end (ie. as if it were a slice)
func (w *Walker) walkOffsetTypeList(ast *syntax.ASTBranch, startOffset, endOffset int) ([]typing.DataType, bool) {
	types := make([]typing.DataType, (ast.Len()-startOffset-endOffset)/2+1)

	for i, item := range ast.Content[startOffset:endOffset] {
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

// walkTypeLabel walks and attempts to extract a data type from a type label. If
// this function fails, it will set `fatalDefError` appropriately.
func (w *Walker) walkTypeLabel(label *syntax.ASTBranch) (typing.DataType, bool) {
	typeCat := label.BranchAt(0)
	switch typeCat.Name {
	case "named_type":
		dt, requiresRef, ok := w.walkNamedType(typeCat)

		// we know that since we are the exterior of walk type label, we will never have the
		// enclosing reference type required as so we can simply return false.
		if requiresRef {
			logging.LogError(
				w.Context,
				fmt.Sprintf("The type `%s` can only be stored by reference here", dt.Repr()),
				logging.LMKTyping,
				typeCat.Position(),
			)
		}

		return dt, ok
	}

	return nil, false
}

// walkNamedType walks a `named_type` node fully.  It returns the type it
// extracts (or nil if no type can be determined), a flag indicating whether or
// not this named type may only be accessed by reference (eg. for self-types),
// and a flag indicating whether or not the type was able to determined/created.
func (w *Walker) walkNamedType(namedTypeLabel *syntax.ASTBranch) (typing.DataType, bool, bool) {
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
	namedType, requiresRef, ok := w.lookupNamedType(rootName, accessedName, rootPos, accessedPos)

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
	}

	return namedType, requiresRef, true
}

// lookupNamedType performs the full look-up of a named type based on the values
// that are extracted during the execution of walkNamedType.  It handles opaque
// types and self-types.  Generics are processed by walkNamedType.  It returns
// the same parameters as walkNamedType.
func (w *Walker) lookupNamedType(rootName, accessedName string, rootPos, accessedPos *logging.TextPosition) (typing.DataType, bool, bool) {
	// NOTE: we don't consider algebraic instances here (statically accessed)
	// because they cannot be used as types.  They are only values so despite
	// using the `::` syntax, they are simply not usable here (and simply saying
	// no package exists is good enough).  This is also why we don't need to
	// consider opaque algebraic instances since such accessing should never
	// occur.

	// if there is no accessed name, than this just a standard symbol access
	if accessedName == "" {
		symbol, ok := w.Lookup(rootName)

		// if the symbol exists in the regular local table
		if ok {
			if symbol.DefKind != common.DefKindTypeDef {
				w.logFatalDefError(
					fmt.Sprintf("Symbol `%s` is not a type", symbol.Name),
					logging.LMKUsage,
					rootPos,
				)

				return nil, false, false
			}

			if w.DeclStatus == common.DSExported {
				if symbol.VisibleExternally() {
					return symbol.Type, false, true
				}

				w.logFatalDefError(
					fmt.Sprintf("Symbol `%s` must be exported to be used in an exported definition", symbol.Name),
					logging.LMKUsage,
					rootPos,
				)

				return nil, false, false
			}

			return symbol.Type, false, true
		} else if w.resolving {
			// if we are resolving, then we need to check for opaque types and
			// self types (checked in two separate if blocks)
			if w.sharedOpaqueSymbol.SrcPackageID == w.SrcPackage.PackageID && w.sharedOpaqueSymbol.Name == rootName {
				return w.sharedOpaqueSymbol.Type, w.opaqueSymbolRequiresRef(), true
			} else if w.selfType != nil && rootName == w.currentDefName {
				w.selfTypeUsed = true
				return w.selfType, w.selfTypeRequiresRef, true
			} else {
				// otherwise, we mark it as unknown and return
				w.unknowns[rootName] = &common.UnknownSymbol{
					Name:     rootName,
					Position: rootPos,
				}

				return nil, false, false
			}
		} else {
			// symbol is unknown, and we are not resolving
			w.LogUndefined(rootName, rootPos)
			return nil, false, false
		}
	} else if w.DeclStatus == common.DSExported {
		w.logFatalDefError(
			"Unable to use implicitly imported symbol in exported definition",
			logging.LMKUsage,
			accessedPos,
		)
	}

	if pkg, ok := w.SrcFile.VisiblePackages[rootName]; ok {
		if symbol, ok := pkg.ImportFromNamespace(accessedName); ok {
			if symbol.DefKind != common.DefKindTypeDef {
				w.logFatalDefError(
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
			if w.sharedOpaqueSymbol.SrcPackageID == pkg.PackageID && w.sharedOpaqueSymbol.Name == accessedName {
				return w.sharedOpaqueSymbol.Type, w.opaqueSymbolRequiresRef(), true
			} else {
				// otherwise, it is just an unknown
				w.unknowns[accessedName] = &common.UnknownSymbol{
					Name:           accessedName,
					Position:       accessedPos,
					ForeignPackage: pkg,
					ImplicitImport: true,
				}
			}
		} else {
			// we are not resolving, so the error must be fatal
			w.LogNotVisibleInPackage(accessedName, rootName, accessedPos)
			return nil, false, false
		}
	}

	w.logFatalDefError(
		fmt.Sprintf("Package `%s` is not defined", rootName),
		logging.LMKName,
		rootPos,
	)
	return nil, false, false
}

// opaqueSymbolRequiresRef checks within the given context whether or not the
// shared opaque symbol must be accessed by reference.  This function assumes
// that the opaque symbol can be used
func (w *Walker) opaqueSymbolRequiresRef() bool {
	if w.sharedOpaqueSymbol.RequiresRef {
		if pkgIDs, ok := w.sharedOpaqueSymbol.DependsOn[w.currentDefName]; ok {
			for _, pkgID := range pkgIDs {
				if pkgID == w.SrcPackage.PackageID {
					return true
				}
			}
		}
	}

	return false
}
