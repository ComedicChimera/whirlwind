package assemble

// PAssembler is the construct responsible for managing package assembly --
// construction of top-level of a package or packages.  It is the implementation
// of compilation Stage 2.
type PAssembler struct {
	// Packages is the map of packages that are being assembled (by ID) along
	// with their associated resolution table.  The package ref is embedded in
	// the resolution table so we don't need to store it two times.
	Packages map[uint]*ResolutionTable

	// DefQueue is the queue of definitions being resolved in all packages
	DefQueue *DefinitionQueue
}

// NewPackageAssembler returns a new PAssembler for the given packages
func NewPackageAssembler(pkgs []*common.WhirlPackage) (*PAssembler) {
	pa := &PAssembler{Packages: make(map[uint]*ResolutionTable)}
	for _, pkg := range pkgs {
		pa.Packages[pkg.PackageID] = &ResolutionTable{CurrPkg: pkg}
	}
	pa.DefQueue = &DefinitionQueue{}
	return pa
}

// Assemble runs the main package assembly algorithm
func (pa *PAssember) Assemble() {
	for pkgid, rtable := range pa.Packages {
		// TODO
		_, _ := pkgid, rtable
	}
}