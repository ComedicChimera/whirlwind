package depm

import (
	"path"

	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// ImportManager is a construct used to faciliate importing and loading as a
// subsidiary of the compiler.  It acts as the main control structure for the
// front-end of the compiler.
type ImportManager struct {
	// DepGraph represents the graph of all the packages used in a given project
	// along with their connections.  It is the main way the compiler will store
	// dependencies and keep track of what imports what.  It is also used to
	// help manage and resolve cyclic dependencies.
	DepGraph map[string]*WhirlPackage

	// RootPackagePath is the path to the main compilation package.  It is used
	// to faciliate absolute imports.
	RootPackagePath string

	// Parser is a reference to the parser created at the start of compilation.
	Parser *syntax.Parser
}

// NewImportManager creates a new ImportManager with the given RootPath and parser
func NewImportManager(p *syntax.Parser, rpp string) *ImportManager {
	im := &ImportManager{
		DepGraph:        make(map[string]*WhirlPackage),
		RootPackagePath: rpp,
		Parser:          p,
	}

	return im
}

// Import is the main function used to faciliate the compilation front-end.  It
// takes a directory to import (relative to RootPackagePath) and performs all
// the necessary steps of importing. This function acts to run the full
// front-end of the compiler for the given node.  It returns a boolean
// indicating whether or not the package was successfully imported as well as
// the package itself.
func (im *ImportManager) Import(pkgpath string) (*WhirlPackage, bool) {
	// TODO: properly handle import cycles
	if depgpkg, ok := im.DepGraph[pkgpath]; ok {
		return depgpkg, true
	}

	pkg, err := im.initPackage(path.Join(im.RootPackagePath, pkgpath))

	if err != nil {
		util.LogMod.LogError(err)
		return nil, false
	}

	// catch/stop for any errors that were caught at the file level as opposed
	// to at the init/loading level (this if branch runs if it is successful)
	if util.LogMod.CanProceed() {

		return pkg, true
	}

	return nil, false
}
