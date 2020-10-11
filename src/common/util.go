package common

// ImportFromNamespace attempts to import a symbol by name from the exported
// namespace of another package.
func (pkg *WhirlPackage) ImportFromNamespace(name string) (*Symbol, bool) {
	return nil, false
}

// AddNode adds a new HIRNode to the HIR root of a file
func (wf *WhirlFile) AddNode(node HIRNode) {
	wf.Root.Elements = append(wf.Root.Elements, node)
}
