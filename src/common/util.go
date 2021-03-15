package common

// ImportFromNamespace attempts to import a symbol by name from the exported
// namespace of another package.
func (pkg *WhirlPackage) ImportFromNamespace(name string) (*Symbol, bool) {
	if sym, ok := pkg.GlobalTable[name]; ok && sym.VisibleExternally() {
		return sym, true
	}

	for _, wfile := range pkg.Files {
		if wsi, ok := wfile.LocalTable[name]; ok && wsi.SymbolRef.VisibleExternally() {
			return wsi.SymbolRef, true
		}
	}

	return nil, false
}

// AddNode adds a new HIRNode to the HIR root of a file
func (wf *WhirlFile) AddNode(node HIRNode) {
	wf.Root.Elements = append(wf.Root.Elements, node)
}

// LookupOpaque looks up a symbol in the opaque symbol table
func (ost OpaqueSymbolTable) LookupOpaque(pkgid uint, name string) (*OpaqueSymbol, bool) {
	if row, ok := ost[pkgid]; ok {
		if osym, ok := row[name]; ok {
			return osym, ok
		}
	}

	return nil, false
}
