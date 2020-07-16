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
		// unable to resolve imports
		if !im.collectImports(pkg) {
			return nil, false
		}

		im.walkPackage(pkg)

		return pkg, true
	}

	return nil, false
}

// collectImports walks the header's of all package collecting their imports
// (both exported and unexported), declares them within the package and adds
// them to the dependency graph.  NOTE: should be called for the package walking
// is attempted (ensures all dependencies are accounted for).
func (im *ImportManager) collectImports(pkg *WhirlPackage) bool {
	// we want to evaluate as many imports as possible so we uses this flag :)
	allImportsResolved := true

	for _, wf := range pkg.Files {
		// top of file is always `file`
	nodeloop:
		for _, node := range wf.AST.Content {
			// all top level are branches
			branch := node.(*syntax.ASTBranch)

			switch branch.Name {
			case "import_stmt":
				// can we all just agree that you need to have a compound
				// assignment operator for booleans?  Please?  Oh and if you
				// thought we could simply use bitwise operators, well you would
				// be wrong because the great Go overloads don't believe in such
				// silly things.  Before someone suggests an if statement, what
				// I have written is literally equivalent to an if statement but
				// slightly more "concise" b/c logical operators short circuit
				// which technically involves a conditional branch (only this
				// one might be slightly faster than that of an if but who
				// cares)
				allImportsResolved = allImportsResolved && im.walkImport(branch, false)
			case "exported_import":
				// this kills me inside too; trust me
				allImportsResolved = allImportsResolved && im.walkImport(
					branch.Content[1].(*syntax.ASTBranch), false)
			// as soon as we encounter one of these blocks, we want to exit
			case "top_level", "export_block":
				// I have written so many of these now, I almost forgot I was
				// writing a `goto` in disguise.  `break` literally does nothing
				// that isn't already implicit in a Go switch statement so why
				// the f*ck does it break the switch and not the loop.  SMH...
				break nodeloop
			}
		}
	}

	return allImportsResolved
}

// walkImport walks an `import_stmt` AST node (because Go is annoying about
// circular imports, this has to be here instead of in the walker).
func (im *ImportManager) walkImport(node *syntax.ASTBranch, exported bool) bool {
	return true
}

// walkPackage walks through all of the files in a package after their imports
// have been collected and resolved.
func (im *ImportManager) walkPackage(pkg *WhirlPackage) bool {
	return true
}
