package build

import (
	"fmt"
	"path"
	"strings"

	"github.com/ComedicChimera/whirlwind/src/analysis"
	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// importPackage is the main function used to faciliate the compilation
// front-end.  It takes a directory to import (relative to buildDirectory) and
// performs all the necessary steps of importing. This function acts to run the
// full front-end of the compiler for the given node.  It returns a boolean
// indicating whether or not the package was successfully imported as well as
// the package itself.  `pkgpath` should be relative to the build directory.
func (c *Compiler) importPackage(pkgpath string) (*common.WhirlPackage, bool) {
	// depGraph stores package path's relatively (non-absolute paths)
	if depgpkg, ok := c.depGraph[pkgpath]; ok {
		return depgpkg, true
	}

	pkg, err := c.initPackage(path.Join(c.buildDirectory, pkgpath))

	if err != nil {
		util.LogMod.LogError(err)
		return nil, false
	}

	// catch/stop for any errors that were caught at the file level as opposed
	// to at the init/loading level (this if branch runs if it is successful)
	if util.LogMod.CanProceed() {
		// make sure proper context is restored/updated before this function returns
		prevPkgID := util.CurrentPackage
		util.CurrentPackage = pkg.PackageID
		defer (func() {
			// make sure that analysis is marked as complete before this function
			// returns (regardless of whether or not it fails)
			pkg.AnalysisDone = true

			// restore global package context
			util.CurrentPackage = prevPkgID
		})()

		// add our package to the dependency graph (with a full path)
		c.depGraph[pkgpath] = pkg

		// unable to resolve imports
		if !c.collectImports(pkg) {
			return nil, false
		}

		c.walkPackage(pkg)

		return pkg, true
	}

	return nil, false
}

// collectImports walks the header's of all package collecting their imports
// (both exported and unexported), declares them within the package and adds
// them to the dependency graph.  NOTE: should be called for the package walking
// is attempted (ensures all dependencies are accounted for).
func (c *Compiler) collectImports(pkg *common.WhirlPackage) bool {
	// we want to evaluate as many imports as possible so we uses this flag :)
	allImportsResolved := true

	for fpath, wf := range pkg.Files {
		// no need to manage context here b/c it will be overridden on the next
		// loop cycle (it will only be inaccurate when errors won't be thrown)
		util.CurrentFile = fpath

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
				allImportsResolved = allImportsResolved && c.walkImport(pkg, branch, false)
			case "exported_import":
				// this kills me inside too; trust me
				allImportsResolved = allImportsResolved && c.walkImport(
					pkg, branch.Content[1].(*syntax.ASTBranch), true)
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
func (c *Compiler) walkImport(currpkg *common.WhirlPackage, node *syntax.ASTBranch, exported bool) bool {
	importedSymbolNames := make(map[string]*util.TextPosition)
	var pkgpath, rename string
	var pkgpathPos *util.TextPosition

	for _, item := range node.Content {
		switch v := item.(type) {
		case *syntax.ASTBranch:
			if v.Name == "package_name" {
				pb := strings.Builder{}

				for _, elem := range v.Content {
					leaf := elem.(*syntax.ASTLeaf)

					if leaf.Kind == syntax.IDENTIFIER {
						pb.WriteString(leaf.Value)
					} else {
						// both `.` and `::` should write a `/`
						pb.WriteRune('/')
					}
				}

				pkgpathPos = v.Position()
				pkgpath = pb.String()
			} else /* only other node is `identifier_list` */ {
				for _, elem := range v.Content {
					leaf := elem.(*syntax.ASTLeaf)

					if leaf.Kind == syntax.IDENTIFIER {
						if _, ok := importedSymbolNames[leaf.Value]; ok {
							util.LogMod.LogError(util.NewWhirlError(
								"Unable to import a symbol multiple times",
								"Import",
								leaf.Position(),
							))

							return false
						}

						importedSymbolNames[leaf.Value] = leaf.Position()
					}
				}
			}
		case *syntax.ASTLeaf:
			switch v.Kind {
			// only use of IDENTIFIER token is in rename
			case syntax.IDENTIFIER:
				rename = v.Value
			case syntax.ELLIPSIS:
				importedSymbolNames["..."] = v.Position()
			}
		}
	}

	// use the current package while it is still value
	currfile := currpkg.Files[util.CurrentFile]
	if pkg, ok := c.importPackage(pkgpath); ok {
		if pkg.PackageID == currpkg.PackageID {
			util.LogMod.LogError(util.NewWhirlError(
				"Package cannot import itself",
				"Import",
				pkgpathPos,
			))

			return false
		}

		importedSymbols := make(map[string]*common.Symbol)

		if len(importedSymbolNames) > 0 {
			// handle full namespace imports
			if pos, ok := importedSymbolNames["..."]; ok {
				if pkg.AnalysisDone {
					for _, imsym := range pkg.GlobalTable {
						if imsym.VisibleExternally() {
							importedSymbols[imsym.Name] = imsym.Import(exported)
						}
					}
				} else {
					util.LogMod.LogError(util.NewWhirlError(
						"Namespace pollution between interdependent packages",
						"Import",
						pos,
					))

					return false
				}
			} else {
				// handle specific symbol imports
				for name, pos := range importedSymbolNames {
					// try to import the current symbol from the outer package
					if imsym, ok := pkg.GlobalTable[name]; ok {
						if imsym.VisibleExternally() {
							importedSymbols[name] = imsym.Import(exported)
						} else {
							util.LogMod.LogError(util.NewWhirlError(
								"Unable to import an internal symbol",
								"Import",
								pos,
							))

							return false
						}
					} else if rsym, ok := pkg.RemoteSymbols[name]; ok {
						// if this symbol has already been requested, use the shared
						// reference so resolution applies generally
						importedSymbols[name] = rsym
					} else {
						// otherwise, create a new remote symbol for the package
						ssym := &common.Symbol{Name: name}
						pkg.RemoteSymbols[name] = ssym
						importedSymbols[name] = ssym
					}
				}

				if wimport, ok := currpkg.ImportTable[pkg.PackageID]; ok {
					for name, importedSym := range importedSymbols {
						wimport.ImportedSymbols[name] = importedSym
					}
				} else {
					currpkg.ImportTable[pkg.PackageID] = &common.WhirlImport{
						PackageRef: pkg, ImportedSymbols: importedSymbols,
					}
				}

				// add our symbols to the local file
				for name, sym := range importedSymbols {
					currfile.LocalTable[name] = sym
				}
			}
		} else {
			name := rename
			if name == "" {
				name = pkg.Name
			}

			currfile.VisiblePackages[name] = pkg
		}
	}

	// calculate the name of the package that we tried to import from its path
	splitPath := strings.Split(pkgpath, "/")
	util.LogMod.LogError(util.NewWhirlError(
		fmt.Sprintf("Unable to import package `%s`", splitPath[len(splitPath)-1]),
		"Import",
		pkgpathPos,
	))

	return false
}

// walkPackage walks through all of the files in a package after their imports
// have been collected and resolved.
func (c *Compiler) walkPackage(pkg *common.WhirlPackage) bool {
	pb := analysis.PackageBuilder{Pkg: pkg}

	return pb.BuildPackage()
}
