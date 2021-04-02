package validate

import (
	"whirlwind/common"
	"whirlwind/typing"
)

// globalLookup attempts to find a symbol in the current package or global
// symbol table. If it succeeds, it returns the symbol it finds.  If it fails,
// it returns false.  The position of the symbol will need to updated AFTER this
// function exits.  This return value will be `nil` if it is local to the
// current package or if the symbol resolved successfully.
func (w *Walker) globalLookup(name string) (*common.Symbol, bool) {
	if sym, ok := w.SrcPackage.GlobalTable[name]; ok {
		return sym, true
	}

	if wsi, ok := w.SrcFile.LocalTable[name]; ok {
		// all unresolved imports should be pruned by this point
		return wsi.SymbolRef, true
	}

	return nil, false
}

// localLookup looks up a symbol from a local scope.  It should be used inside
// function bodies and local expressions.  It also fully implements shadowing.
func (w *Walker) localLookup(name string) (*common.Symbol, bool) {
	// go through the scope stacks in reverse order (to implement shadowing)
	for i := len(w.scopeStack) - 1; i >= 0; i-- {
		scope := w.scopeStack[i]

		if sym, ok := scope.Symbols[name]; ok {
			return sym, true
		}

		// arguments should always be shadowed by local variables
		for _, arg := range scope.FuncCtx.Args {
			if arg.Name == name {
				return &common.Symbol{
					Name:       name,
					Type:       arg.Val.Type,
					Constant:   arg.Val.Constant,
					DeclStatus: common.DSLocal,
					DefKind:    common.DefKindNamedValue,
				}, true
			}
		}
	}

	return w.globalLookup(name)
}

// define defines a new symbol in the global namespace of a package (returns false
// if the symbol if already defined).  It does not log an error.
func (w *Walker) define(sym *common.Symbol) bool {
	if _, ok := w.SrcPackage.GlobalTable[sym.Name]; ok {
		return false
	}

	// local imports also need to be accounted for
	if _, ok := w.SrcFile.LocalTable[sym.Name]; ok {
		return false
	}

	// as do visible packages: can cause idiomatic conflicts (even if compiler could technically
	// resolve them based on usage -- sometimes)
	if _, ok := w.SrcFile.VisiblePackages[sym.Name]; ok {
		return false
	}

	// if it is not already defined, stick it in the global table
	w.SrcPackage.GlobalTable[sym.Name] = sym
	return true
}

// implicitImport attempts to perform an implicit import of a symbol from a visible package
func (w *Walker) implicitImport(ipkg *common.WhirlPackage, name string) (*common.Symbol, bool) {
	// avoid repeated imports if possible
	if sym, ok := w.SrcPackage.ImportTable[ipkg.PackageID].ImportedSymbols[name]; ok {
		return sym, true
	}

	nsym, ok := ipkg.ImportFromNamespace(name)

	// update entry in import table as necessary
	if ok {
		w.SrcPackage.ImportTable[ipkg.PackageID].ImportedSymbols[name] = nsym

	}

	return nsym, ok
}

// getCoreType loads a core/prelude-defined type from the local table
func (w *Walker) getCoreType(name string) typing.DataType {
	return w.SrcFile.LocalTable[name].SymbolRef.Type
}
