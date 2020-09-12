package build

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/util"
)

/*
IMPORT ALGORITHM
----------------
1. Construct a directed, cyclic graph of all of the packages required to build
   the root package with their associated dependencies. All packages should be
   initialized in this stage.

2. For each package in the graph, determine if that package is in a cycle with
   another package.  If so, cross-resolve the symbols of the packages across the
   entire cycle (by considering their global tables spliced).  Otherwise,
   cross-resolve the symbols of the current package exclusively.

   a) By cross-resolve, we mean resolve the symbols nonlinearly such that
   definitions that are provided out of proper order can be reorganized sensibly
   without having to late-resolve any symbols.

   b) By splicing the tables of mutually dependent packages, we mean perform
   cross-resolution as if the packages were sharing a symbol table although they
   are not ultimately.  Note that this method of resolution does not corrupt the
   integrity of any package's namespace as it is being resolved.

   c) All unresolved or unresolvable symbols should be identified in this stage.

   d) Every symbol resolved implies that an satisfactory definition was found for
   it and said definition has undergone top-level analysis (so as to produce as
   a top-level HIR node).

3. Extract and evaluate all the predicates of the top-level definitions.
   Subsequently, analyze any generates produced.  After this phase, the package
   is said to "imported".  However, it has yet to be "built" (-- "compiled").

   a) Final target code generation will be accomplished at a later stage (this
   is what is referred to by "built").

   b) This algorithm's completion also connotes the full completion of analysis.
*/

// initDependencies extracts, parses, and evaluates all the imports of an
// already initialized package (performing step 1 of the Import Algorithm)
func (c *Compiler) initDependencies(pkg *common.WhirlPackage) bool {
   util.CurrentPackageID = pkg.PackageID
   defer func() {
      // ensure current package context is restored when this function returns
      util.CurrentPackageID = pkg.PackageID
   }

	for fpath, file := range pkg.Files {
      util.CurrentFile = fpath

	fileastloop:
		for i, item := range file.AST.Content {
			// attach the prelude for a file as necessary
			c.attachPrelude(pkg, file)

			if tbranch, ok := item.(*syntax.ASTBranch); ok {
				switch tbranch.Name {
				case "import_stmt":
					if !c.processImport(pkg, file, tbranch, false) {
						return false
					}
				case "export_stmt":
					if !c.processImport(pkg, file, tbranch, true) {
						return false
					}
				default:
					// trim off all the already processed content (shouldn't need
					// to refer to it again)
					file.AST.Content = file.AST.Content[i+1:]

					break fileastloop
				}
			}
		}
	}

	return true
}

// processImport walks and evaluates a given import for the current package
func (c *Compiler) processImport(pkg *common.WhirlPackage, file *common.WhirlFile, ibranch *syntax.ASTBranch, exported bool) bool {
	var relPath, rename string
	var pathPosition, renamePosition *util.TextPosition
	var importedSymbols map[string]*util.TextPosition

	// walk the import/export statement and extract all meaningful information
	for _, item := range ibranch.Content {
		switch v := item.(type) {
		case *syntax.ASTLeaf:
			switch v.Kind {
			case syntax.IDENTIFIER:
				// only raw IDENTIFIER token present in `import_stmt` and `export_stmt`
				// is the package rename (after AS)
				rename = v.Value
				renamePosition = v.Position()
			case syntax.ELLIPSIS:
				// the `...` in importedSymbols represents a full-namespace import
				importedSymbols["..."] = v.Position()
			}
		case *syntax.ASTBranch:
			// only `identifier_list` is a list of imported symbols
			if v.Name == "identifier_list" {
				for _, elem := range v.Content {
					if leaf := elem.(*syntax.ASTLeaf); leaf.Kind == syntax.IDENTIFIER {
						importedSymbols[leaf.Value] = elem.Position()
					}
				}
			} else /* `package_name` */ {
				pathBuilder := strings.Builder{}

				for _, elem := range v.Content {
					leaf := elem.(*syntax.ASTLeaf)
					switch leaf.Kind {
					case syntax.GETNAME, syntax.DOT:
						// `.` just means access from build directory [so `/` :)]
						pathBuilder.WriteRune('/')
					case syntax.IDENTIFIER:
						pathBuilder.WriteString(leaf.Value)
					}
				}

				relPath = pathBuilder.String()
				pathPosition = v.Position()
			}
		}
	}

	// calculate the absolute path to the package
	abspath := c.getPackagePath(relPath)
	if abspath == "" {
		util.ThrowError(
			fmt.Sprintf("Unable to locate package at path `%s`", relPath),
			"Import",
			pathPosition,
		)

		return false
	}

	// either access the already initialized package or init a new one
	newpkg, ok := c.depGraph[getPackageID(abspath)]
	if !ok {
		var err error
		newpkg, err = c.initPackage(abspath)
		if err != nil {
			util.LogMod.LogError(err)
			return false
		}
	}

	// update the current package's imports
	if wimport, ok := pkg.ImportTable[newpkg.PackageID]; ok {
		if len(importedSymbols) > 0 {
			for name, pos := range importedSymbols {
				// `...` implies a full namespace import (stored in
				// `importedSymbols`)
				if name == "..." {
					wimport.NamespaceImport = true
					break
				}

				if wsi, ok := wimport.ImportedSymbols[name]; ok {
					wsi.Positions = append(wsi.Positions, pos)
				} else {
					// `SymbolRef` is initialized to `nil` (b/c it is unknown)
					wimport.ImportedSymbols[name] = &common.WhirlSymbolImport{
						Name:      name,
						Positions: []*util.TextPosition{pos},
					}
				}
			}
		}
	} else /* add a new import entry for the current package */ {
		wimport = &common.WhirlImport{PackageRef: newpkg}

		if len(importedSymbols) > 0 {
			for name, pos := range importedSymbols {
				// as before, `...` implies a full namespace import
				if name == "..." {
					wimport.NamespaceImport = true
					break
				}

				// all symbols in a new import are new/unique
				wimport.ImportedSymbols[name] = &common.WhirlSymbolImport{
					Name:      name,
					Positions: []*util.TextPosition{pos},
				}
			}
		}

		// add the new import to the current package's import table
		pkg.ImportTable[newpkg.PackageID] = wimport
	}

	// make the package visible if it is imported as a named entity
	if len(importedSymbols) == 0 {
		// rename supercedes original name
		name := rename
		if name == "" {
			name = newpkg.Name
		}

		file.VisiblePackages[name] = newpkg
   }

	return c.initDependencies(newPkg)
}

// getPackagePath determines, from a relative path, the absolute path to a
// package
func (c *Compiler) getPackagePath(relpath string) string {
	validPath := func(abspath string) bool {
		fi, err := os.Stat(abspath)

		if err == nil {
			return fi.IsDir()
		}

		return false
	}

	bdAbsPath := filepath.Join(c.buildDirectory, relpath)
	if validPath(bdAbsPath) {
		return bdAbsPath
	}

	for _, ldirpath := range c.localPkgDirectories {
		localAbsPath, err := filepath.Abs(filepath.Join(ldirpath, relpath))

		if err != nil {
			continue
		}

		if validPath(localAbsPath) {
			return localAbsPath
		}
	}

	pubdirabspath := filepath.Join(c.whirlpath, "lib/pub", relpath)
	if validPath(pubdirabspath) {
		return pubdirabspath
	}

	stddirabspath := filepath.Join(c.whirlpath, "lib/std", relpath)
	if validPath(stddirabspath) {
		return stddirabspath
	}

	return ""
}
