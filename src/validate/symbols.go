package validate

import "whirlwind/common"

// Lookup attempts to find a symbol in the current package or global symbol
// table. If it succeeds, it returns the symbol it finds.  If it fails, it
// returns false, and marks it as unknown as necessary.  The position of the
// symbol will need to updated AFTER this function exits.  It also returns the
// the foreign package the symbol was located in (if it was imported via a named
// symbol import) so that it can be properly reported as unknown.  This return
// value will be `nil` if it is local to the current package or if the symbol
// resolved successfully.  Externally visible for use in symbol resolution.
func (w *Walker) Lookup(name string) (*common.Symbol, *common.WhirlPackage, bool) {
	if sym, ok := w.SrcPackage.GlobalTable[name]; ok {
		return sym, nil, true
	}

	if wsi, ok := w.SrcFile.LocalTable[name]; ok {
		// if the symbol has no name, then it was accessed via import but it has
		// yet to be updated -- which we will do here
		if wsi.SymbolRef.Name == "" {
			// check to see if the symbol is already defined in the remote
			// package.  If it is, just update the symbol reference with it.
			if rsym, ok := wsi.SrcPackage.ImportFromNamespace(name); ok {
				ds := wsi.SymbolRef.DeclStatus
				*wsi.SymbolRef = *rsym
				wsi.SymbolRef.DeclStatus = ds
				return rsym, wsi.SrcPackage, true
			}

			// we do not want to remove the symbol import yet as it may still be
			// resolveable.  This lookup should only happen once or twice per
			// definition -- once it is updated, it will never happen again

			return nil, wsi.SrcPackage, false
		}

		return wsi.SymbolRef, nil, true
	}

	return nil, nil, false
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

	// update the unknowns as necessary
	if !ok && w.unknowns != nil {
		w.unknowns[name] = &common.UnknownSymbol{
			Name:           name,
			ForeignPackage: ipkg,
			ImplicitImport: true,
		}
	}

	// update entry in import table
	w.SrcPackage.ImportTable[ipkg.PackageID].ImportedSymbols[name] = nsym

	return nsym, ok
}

// clearUnknowns resets the map of unknowns before another definition is analyzed
func (w *Walker) clearUnknowns() {
	w.unknowns = make(map[string]*common.UnknownSymbol)
}
