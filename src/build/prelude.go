package build

import (
	"fmt"
	"path/filepath"

	"whirlwind/common"
	"whirlwind/logging"
	"whirlwind/mods"
)

// preludeImports is a list of the imported prelude packages (after they are loaded)
var preludeImports = make(map[string]*common.WhirlPackage)

// preludeImportPatterns enumerates all of the prelude modules and the symbols they expose
// TODO: fix this syntax to provide an actual list of the symbols we import (or
// have the compiler walk through all of the exported symbols)
var preludeImportPatterns = map[string][]string{
	// TODO: figure out remaining prelude import patterns
	"core":         {"clamp"},
	"core/runtime": {"temp"},
	"core/types":   {"Iterator", "Numeric", "Integral", "Floating", "Comparable", "Ord"},
}

// initPrelude initializes the prelude packages thereby adding them to the
// dependency graph and making them available to all other packages being built
func (c *Compiler) initPrelude() bool {
	// load the core module to be shared by all core/prelude packages
	coreMod, ok := mods.LoadModule(filepath.Join(c.whirlpath, "lib/std/core"))

	if !ok {
		// the core module is essential for building the core library
		logging.LogFatal("Unable to load core module")
	}

	// initialize each prelude package so that they are present in the
	// dependency graph
	for stdpkgname := range preludeImportPatterns {
		preludePath := filepath.Join(c.whirlpath, "lib/std", stdpkgname)

		// it is possible for one import to be initialized as a result of
		// another before it so we have to check and make sure we get to correct
		// package reference
		pkgID := getPackageID(preludePath)
		if pkg, ok := c.depGraph[pkgID]; ok {
			preludeImports[stdpkgname] = pkg
		} else {
			// if anything happens during initialization of this packages, we
			// throw a fatal compiler error: they are NECESSARY for compilation
			npkg, err, initOk := c.initPackage(preludePath, coreMod)
			if !initOk {
				if err != nil {
					logging.LogStdError(err)
				}

				logging.LogFatal(fmt.Sprintf("Unable to load necessary prelude package: `%s`", stdpkgname))
			}

			preludeImports[stdpkgname] = npkg
		}
	}

	// initialize the `core` prelude package dependencies which implicitly
	// initializes all the others dependencies
	if !c.initDependencies(preludeImports["core"]) {
		return false
	}

	return logging.ShouldProceed()
}

// attachPrelude adds in all additional prelude imports (determined based on a
// file's host package). This function does not proceed beyond stage 1 of
// importing for the prelude (all prelude imports can be treated as normal
// package imports beyond this point).  All errors that occur in this function
// are considered fatal (as they have no direct text position).
func (c *Compiler) attachPrelude(pkg *common.WhirlPackage, file *common.WhirlFile) {
	for stdpkgname, piPkg := range preludeImports {
		if pkg.PackageID != piPkg.PackageID {
			// `core` is the name of the prelude utils directory and so we skip
			// this import if the specific file requests it (via. `!! no_util`)
			if _, ok := file.MetadataTags["no_util"]; ok && stdpkgname == "core" {
				continue
			}

			// create a map of imported symbols (all of which will have `nil`
			// positions). If the compiler needs to throw an error involving
			// these imports specifically and it encounters a `nil` position, it
			// should consider the error fatal.
			importedSymbols := make(map[string]*logging.TextPosition)
			for _, importedSymbolName := range preludeImportPatterns[stdpkgname] {
				importedSymbols[importedSymbolName] = nil
			}

			// prelude packages are never imported by name so we can leave the
			// fields that relate to names and renames blank
			c.attachPackageToFile(pkg, file, piPkg, importedSymbols, "", nil)
		}
	}
}
