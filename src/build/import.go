package build

import (
	"fmt"
	"os"
	"path/filepath"
	"strings"

	"whirlwind/common"
	"whirlwind/logging"
	"whirlwind/mods"
	"whirlwind/syntax"
	"whirlwind/typing"
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
	// avoid initializing the dependencies of a package multiple times
	if pkg.Initialized {
		return true
	}

	c.lctx.PackageID = pkg.PackageID
	defer (func() {
		// ensure current package context is restored when this function returns
		c.lctx.PackageID = pkg.PackageID
	})()

	// set our initialization flag before we start recurring
	pkg.Initialized = true

	for fpath, file := range pkg.Files {
		c.lctx.FilePath = fpath

		// attach the prelude for a file as necessary
		c.attachPrelude(pkg, file)

		for i, item := range file.AST.Content {
			if tbranch, ok := item.(*syntax.ASTBranch); ok && tbranch.Name == "import_stmt" {
				if !c.processImport(pkg, file, tbranch) {
					return false
				}
			} else {
				// trim off all the already processed content (shouldn't need to
				// refer to it again)
				file.AST.Content = file.AST.Content[i:]

				break
			}
		}
	}

	return true
}

// processImport walks and evaluates a given import for the current package
func (c *Compiler) processImport(pkg *common.WhirlPackage, file *common.WhirlFile, ibranch *syntax.ASTBranch) bool {
	var relPath, rename string
	var pathPosition, namePosition *logging.TextPosition
	importedSymbols := make(map[string]*logging.TextPosition)

	// walk the import/export statement and extract all meaningful information
	for _, item := range ibranch.Content {
		switch v := item.(type) {
		case *syntax.ASTLeaf:
			switch v.Kind {
			case syntax.IDENTIFIER:
				// only raw IDENTIFIER token present in `import_stmt` is the
				// package rename (after AS)
				rename = v.Value
				namePosition = v.Position()
			}
		case *syntax.ASTBranch:
			// only `identifier_list` is a list of imported symbols
			if v.Name == "identifier_list" {
				for _, elem := range v.Content {
					if leaf := elem.(*syntax.ASTLeaf); leaf.Kind == syntax.IDENTIFIER {
						importedSymbols[leaf.Value] = elem.Position()
					}
				}

				// `namePosition` will be overridden with the position of the
				// rename `IDENTIFIER` token if one exists
				namePosition = v.Position()
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
	abspath := c.getPackagePath(pkg.ParentModule, relPath)
	if abspath == "" {
		logging.LogCompileError(
			c.lctx,
			fmt.Sprintf("Unable to locate package at path `%s`", relPath),
			logging.LMKImport,
			pathPosition,
		)

		return false
	}

	// either access the already initialized package or init a new one
	newpkg, ok := c.depGraph[getPackageID(abspath)]
	if !ok {
		var initOk bool

		// pass the parent module of the package importing to the new package if
		// no parent module exists
		newpkg, initOk = c.initPackage(abspath, pkg.ParentModule)
		if !initOk {
			return false
		}
	}

	// check for self-imports (which are illegal)
	if pkg.PackageID == newpkg.PackageID {
		logging.LogCompileError(
			c.lctx,
			fmt.Sprintf("Package `%s` cannot import itself", pkg.Name),
			logging.LMKImport,
			namePosition,
		)
	}

	// now that we have processed the import, we can attach the package to a file
	return c.attachPackageToFile(pkg, file, newpkg, importedSymbols, rename, namePosition)
}

// getPackagePath determines, from a relative path, the absolute path to a
// package (from module dir, local pkg dir, global/pub pkg dir or std pkg dir)
func (c *Compiler) getPackagePath(parentModule *mods.Module, relpath string) string {
	validPath := func(abspath string) bool {
		fi, err := os.Stat(abspath)

		if err == nil {
			return fi.IsDir()
		}

		return false
	}

	if strings.HasPrefix(relpath, parentModule.Name) {
		modRelPath := strings.TrimPrefix(relpath, parentModule.Name+"/")

		// path overrides should already be checked and made absolute when
		// module is loaded
		if pathOverride, ok := parentModule.PathOverrides[modRelPath]; ok {
			return pathOverride
		}

		bdAbsPath := filepath.Join(parentModule.Path, modRelPath)
		if validPath(bdAbsPath) {
			return bdAbsPath
		}
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

// attachPackageToFile attaches an already loaded file to a package (completing
// first stage of importing package for that file).  `rootPkg` is the current
// package (package the file is in) and `newPkg` is the new package (package
// being attached).  NOTE: `rename` can be blank if the package is not renamed,
// `namePosition` should point whatever token or branch is used name of the
// package is derived from (not just rename).
func (c *Compiler) attachPackageToFile(rootPkg *common.WhirlPackage, file *common.WhirlFile,
	newPkg *common.WhirlPackage, importedSymbols map[string]*logging.TextPosition, rename string, namePosition *logging.TextPosition) bool {

	// update the current package's imports
	if wimport, ok := rootPkg.ImportTable[newPkg.PackageID]; ok {
		if len(importedSymbols) > 0 {
			for name, pos := range importedSymbols {
				if _, ok := file.LocalTable[name]; ok {
					logging.LogCompileError(c.lctx, fmt.Sprintf("Symbol `%s` defined multiple times", name), logging.LMKName, pos)
					return false
				}

				if wisym, ok := wimport.ImportedSymbols[name]; ok {
					file.LocalTable[name] = &common.WhirlSymbolImport{
						SymbolRef:  wisym,
						Position:   pos,
						SrcPackage: newPkg,
					}
				} else {
					// create a blank shared symbol reference
					sref := &common.Symbol{DeclStatus: common.DSRemote}

					// add to the import and the file's local table
					wimport.ImportedSymbols[name] = sref
					file.LocalTable[name] = &common.WhirlSymbolImport{
						SymbolRef:  sref,
						Position:   pos,
						SrcPackage: newPkg,
					}
				}
			}
		}
	} else /* add a new import entry for the current package */ {
		wimport = &common.WhirlImport{PackageRef: newPkg, ImportedSymbols: make(map[string]*common.Symbol)}

		if len(importedSymbols) > 0 {
			for name, pos := range importedSymbols {
				// create a blank shared symbol reference with the appropriate
				// export/import status
				sref := &common.Symbol{DeclStatus: common.DSRemote}

				// add to the import and the file's local table
				wimport.ImportedSymbols[name] = sref
				file.LocalTable[name] = &common.WhirlSymbolImport{
					SymbolRef:  sref,
					Position:   pos,
					SrcPackage: newPkg,
				}
			}
		}

		// add the new import to the current package's import table
		rootPkg.ImportTable[newPkg.PackageID] = wimport
	}

	// make the package visible if it is imported as a named entity
	if len(importedSymbols) == 0 {
		// rename supercedes original name
		name := rename
		if name == "" {
			name = newPkg.Name
		}

		if _, ok := file.VisiblePackages[name]; ok {
			logging.LogCompileError(
				c.lctx,
				fmt.Sprintf("Multiple packages imported with the name `%s`", name),
				logging.LMKImport,
				namePosition,
			)

			return false
		}

		file.VisiblePackages[name] = newPkg
	}

	// import all exported bindings
	if !c.importBindings(newPkg, file, rootPkg, namePosition) {
		return false
	}

	// import all the exported operators
	if !c.importOperators(newPkg, file, rootPkg, namePosition) {
		return false
	}

	// we need to recursively initialize dependencies
	return c.initDependencies(newPkg)
}

// importBindings lifts all exported bindings of an imported package into the
// local namespace of current file.  It logs any conflicts it finds and returns
// an appropriate flag boolean.
func (c *Compiler) importBindings(srcpkg *common.WhirlPackage, destfile *common.WhirlFile, destpkg *common.WhirlPackage, pos *logging.TextPosition) bool {
	logBindingConflictError := func(mname string, binding *typing.Binding) {
		logging.LogCompileError(
			c.lctx,
			fmt.Sprintf("Unable to import package `%s`; multiple implementations given for method `%s` bound to `%s`",
				srcpkg.Name,
				mname,
				binding.MatchType.Repr(),
			),
			logging.LMKImport,
			pos,
		)
	}

	for _, binding := range srcpkg.GlobalBindings.Bindings {
		if binding.Exported {
			if mname, isConflict := destfile.LocalBindings.CheckBindingConflicts(binding); isConflict {
				logBindingConflictError(mname, binding)
				return false
			}

			if mname, isConflict := destpkg.GlobalBindings.CheckBindingConflicts(binding); isConflict {
				logBindingConflictError(mname, binding)
				return false
			}

			destfile.LocalBindings.Bindings = append(destfile.LocalBindings.Bindings, binding.PrivateCopy())
		}
	}

	return true
}

// importOperators lifts all exported operators of an imported package into the
// local namespace of current file.  It logs any conflicts it finds and returns
// an appropriate flag boolean.
func (c *Compiler) importOperators(srcpkg *common.WhirlPackage, destfile *common.WhirlFile, destpkg *common.WhirlPackage, pos *logging.TextPosition) bool {
	for opkind, gopdefs := range srcpkg.OperatorDefinitions {
		for _, gopdef := range gopdefs {
			if gopdef.Exported {
				if sig, isConflict := destpkg.CheckOperatorConflicts(destfile, opkind, gopdef.Signature); isConflict {
					logging.LogCompileError(
						c.lctx,
						fmt.Sprintf("Unable to import package `%s`; conflicting operator definitions for `%s` of `%s` and `%s`",
							srcpkg.Name,
							syntax.GetOperatorTokenValueByKind(opkind),
							sig.Repr(),
							gopdef.Signature.Repr(),
						),
						logging.LMKImport,
						pos,
					)

					return false
				}
			}
		}
	}

	return true
}
