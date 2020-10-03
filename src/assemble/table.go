package assemble

import "github.com/ComedicChimera/whirlwind/src/common"

// SymbolTable represents an "table" or record used for storing symbols.  It can
// be scoped, spread over multiple packages, or anything in between.  It is used
// to unify the APIs of the package assembler and package validator.
type SymbolTable interface {
	Lookup(name string) (*common.Symbol, bool)
	StaticGet(src, elem string) (*common.Symbol, bool)
	Declare(sym *common.Symbol) bool
}

// ResolutionTable represents the symbol table being constructed during package
// assembly.  It stores unknown symbols as they are requested so that they can
// be associated with the given definition that requires them (pool all the
// unknowns of a definition).
type ResolutionTable struct {
	// CurrPkg represents the package containing definition being operated on
	CurrPkg *common.WhirlPackage

	// CurrFile represents the file containing definition being operated on
	CurrFile *common.WhirlFile

	// Unknowns is a map of a symbols that are currently unknown but required.
	// The boolean field indicates whether or not the symbol is imported and
	// undefined.
	Unknowns map[string]bool
}

// Lookup attempts to find a symbol in the current package or global symbol
// table. If it is unsuccessful, it marks the symbol as unknown.  Otherwise, it
// returns the symbol it finds.
func (rt *ResolutionTable) Lookup(name string) (*common.Symbol, bool) {
	for _, nipkg := range rt.CurrFile.NamespaceImports {
		if sym, ok := importFromNamespace(nipkg, name); ok {
			return sym, true
		}
	}

	if sym, ok := rt.CurrPkg.GlobalTable[name]; ok {
		return sym, true
	}

	if sym, ok := rt.CurrFile.LocalTable[name]; ok {
		// if the symbol has no name, then it is undefined (accessed via import)
		if sym.Name == "" {
			rt.Unknowns[name] = true
			return nil, false
		}

		return sym, true
	}

	rt.Unknowns[name] = false
	return nil, false
}

// Define defines a new symbol in the global namespace of a package (returns false
// if the symbol if already defined).
func (rt *ResolutionTable) Define(sym *common.Symbol) bool {
	if _, ok := rt.CurrPkg.GlobalTable[sym.Name]; ok {
		return false
	}

	// local imports also need to be accounted for
	if _, ok := rt.CurrFile.LocalTable[sym.Name]; ok {
		return false
	}

	// as do visible packages: can cause idiomatic conflicts (even if compiler could technically
	// resolve them based on usage -- sometimes)
	if _, ok := rt.CurrFile.VisiblePackages[sym.Name]; ok {
		return false
	}

	// if it is not already defined, stick it in the global table
	rt.CurrPkg.GlobalTable[sym.Name] = sym
	return true
}

// ClearUnknowns resets the map of unknowns before another definition is analyzed
func (rt *ResolutionTable) ClearUnknowns() {
	rt.Unknowns = make(map[string]bool)
}

// importFromNamespace attempts to import a symbol by name from the exported
// namespace of another package (given as the argument).
func importFromNamespace(srcPkg *common.WhirlPackage, name string) (*common.Symbol, bool) {
	return nil, false
}
