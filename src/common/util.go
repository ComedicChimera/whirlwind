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

		for pkg, exported := range wfile.NamespaceImports {
			if exported {
				if sym, ok := pkg.ImportFromNamespace(name); ok {
					return sym, true
				}
			}
		}
	}

	return nil, false
}

// AddNode adds a new HIRNode to the HIR root of a file
func (wf *WhirlFile) AddNode(node HIRNode) {
	wf.Root.Elements = append(wf.Root.Elements, node)
}
