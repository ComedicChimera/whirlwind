package validate

import "github.com/ComedicChimera/whirlwind/src/common"

// Lookup attempts to find a symbol in the current package or global symbol
// table. If it succeeds, it returns the symbol it finds.  If it fails, it
// returns false, and marks it as unknown as necessary.  The position of the
// symbol will need to updated AFTER this function exits.  Externally visible
// for use in symbol resolution.
func (w *Walker) Lookup(name string) (*common.Symbol, bool) {
	if sym, ok := w.SrcPackage.GlobalTable[name]; ok {
		return sym, true
	}

	if wsi, ok := w.SrcFile.LocalTable[name]; ok {
		// if the symbol has no name, then it is undefined (accessed via import)
		if wsi.SymbolRef.Name == "" {
			if w.unknowns != nil {
				w.unknowns[name] = &common.UnknownSymbol{
					Name:           name,
					ForeignPackage: wsi.SrcPackage,
				}
			}

			return nil, false
		}

		return wsi.SymbolRef, true
	}

	if w.unknowns != nil {
		w.unknowns[name] = &common.UnknownSymbol{Name: name}
	}

	return nil, false
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
