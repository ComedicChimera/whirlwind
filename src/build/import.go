package build

import (
	"fmt"
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

	pkg, err := c.initPackage(pkgpath)

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

		return pkg, c.walkPackage(pkg)
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
			case "export_stmt":
				// since `export_stmt` is formatted about the same as an
				// `import_stmt` for symbols, they can be used together.
				allImportsResolved = allImportsResolved && c.walkImport(pkg, branch, true)
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

// walkImport walks an `import_stmt` OR an `export_stmt` (since they are very
// similar) AST node (because Go is annoying about circular imports, this has to
// be here instead of in the walker).
func (c *Compiler) walkImport(currpkg *common.WhirlPackage, node *syntax.ASTBranch, exported bool) bool {
	// extract all of the information about the `import_stmt`
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
							util.ThrowError(
								fmt.Sprintf("Unable to import symbol `%s` multiple times", leaf.Value),
								"Import",
								leaf.Position(),
							)

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

	// attempt to import the package
	if pkg, ok := c.importPackage(pkgpath); ok {
		// catch unresolvable cyclic imports
		if pkg.PackageID == currpkg.PackageID {
			util.ThrowError(
				"Package cannot import itself",
				"Import",
				pkgpathPos,
			)

			return false
		}

		// store a map of all the symbols imported by package
		// (not the names => just the `importedSymbols`)
		importedSymbols := make(map[string]*common.Symbol)

		// if there are any symbol names, attempt to import them
		if len(importedSymbolNames) > 0 {
			// handle full namespace imports
			if pos, ok := importedSymbolNames["..."]; ok {
				// if the package has already been analyzed, then we are able to
				// do a full namespace import.  Otherwise, we don't know what
				// symbols are declared and so we can't properly to a full
				// namespace import.
				if pkg.AnalysisDone {
					for _, imsym := range pkg.GlobalTable {
						if imsym.VisibleExternally() {
							importedSymbols[imsym.Name] = imsym.Import(exported)
						}
					}
				} else {
					// This error conveys the idea of a full namespace import
					// failing. Full namespace imports are explicitly namespace
					// pollution so this error makes sense.
					util.ThrowError(
						"Namespace pollution between interdependent packages",
						"Import",
						pos,
					)

					return false
				}
			} else {
				// handle specific symbol imports
				for name, pos := range importedSymbolNames {
					// try to import the current symbol from the outer package
					if imsym, ok := pkg.GlobalTable[name]; ok {
						// if it is not visible externally, we have to throw an
						// error because such an imports are invalid.
						if imsym.VisibleExternally() {
							importedSymbols[name] = imsym.Import(exported)
						} else {
							util.ThrowError(
								fmt.Sprintf("Unable to import symbol `%s` from package `%s`", name, pkg.Name),
								"Import",
								pos,
							)

							return false
						}
					} else if rsym, ok := pkg.RemoteSymbols[name]; ok {
						// if this symbol has already been requested, use the
						// shared reference so resolution applies generally.
						// This doesn't guarantee resolution, just that
						// resolution is possible.
						importedSymbols[name] = rsym.SymRef
					} else {
						// otherwise, create a new remote symbol for the package
						ssym := &common.Symbol{Name: name}
						pkg.RemoteSymbols[name] = &common.RemoteSymbol{
							SymRef:   ssym,
							Position: pos,
						}
						importedSymbols[name] = ssym
					}
				}

				// add an appropriate import entry to the import table for the
				// current package (so that the import is tracked)
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
					// resolve remote symbols based on imports
					if exported {
						if rsym, ok := currpkg.RemoteSymbols[name]; ok {
							*rsym.SymRef = *sym
							delete(currpkg.RemoteSymbols, name)
						}
					}

					currfile.LocalTable[name] = sym
				}
			}
		} else {
			// otherwise, assume we are doing a full package import
			name := rename
			if name == "" {
				name = pkg.Name
			}

			// add an appropriate import entry to the import table
			if _, ok := currpkg.ImportTable[pkg.PackageID]; !ok {
				currpkg.ImportTable[pkg.PackageID] = &common.WhirlImport{
					PackageRef: pkg, ImportedSymbols: make(map[string]*common.Symbol),
				}
			}

			// make this usage appropriately in visible packages
			currfile.VisiblePackages[name] = pkg
		}
	}

	// if a package can't be imported, it will be logged later so we can fail
	// silently here (won't progress to the next stage anyway)
	return false
}

// walkPackage walks through all of the files in a package after their imports
// have been collected and resolved.
func (c *Compiler) walkPackage(pkg *common.WhirlPackage) bool {
	pb := analysis.PackageBuilder{Pkg: pkg}

	return pb.BuildPackage()
}
