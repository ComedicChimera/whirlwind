package common

import (
	"whirlwind/typing"
)

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

// CheckOperatorConflicts takes in a file and an operator kind, an operator
// signature, and attempts to determine if any preexisting operator definitions
// conflict with that signature.  If a conflict exists, a type signature (that
// conflicts) is returned (for logging) and the return flag is set to true.
// Otherwise, nil is returned and the return flag is false.
func (pkg *WhirlPackage) CheckOperatorConflicts(wf *WhirlFile, opkind int, signature typing.DataType) (typing.DataType, bool) {
	// check for local operator conflicts
	for _, sig := range wf.LocalOperatorDefinitions[opkind] {
		if typing.OperatorsConflict(signature, sig) {
			return sig, true
		}
	}

	// check for global operator conflicts
	for _, gopdef := range pkg.OperatorDefinitions[opkind] {
		if typing.OperatorsConflict(signature, gopdef.Signature) {
			return gopdef.Signature, true
		}
	}

	return nil, false
}
