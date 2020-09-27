package assemble

// Assembler is the construct responsible for managing package assembly --
// construction of top-level of a package or packages.  It is the implementation
// of compilation Stage 2.
type Assembler struct {
	// Packages is the map of packages that are being assembled (by ID) along
	// with their associated resolution table.  The package ref is embedded in
	// the resolution table so we don't need to store it two times.
	Packages map[uint]*ResolutionTable

	// DefQueue is the queue of definitions being resolved in all packages
	DefQueue *DefinitionQueue
}
