package depm

// DependencyGraph represents the graph of all the packages used in a given
// project along with their connections.  It is the main way the compiler will
// store dependencies and keep track of what imports what.  It is also used to
// help manage and resolve cyclic dependencies.
type DependencyGraph map[string]*WhirlPackage

// Import is main algorithm for package dependency management.  It takes a
// pre-initialized package (with all files loaded), extracts definitions,
// constructs its global symbol table as well as its file-local symbol table,
// and handles all imports and exports for the package.  Note that the package
// is assumed to already have an entry in the dependency graph.
func (depG DependencyGraph) Import(pkg *WhirlPackage) {

}
