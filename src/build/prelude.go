package build

import (
	"fmt"
	"path/filepath"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// addPrelude performs the prelude imports necessary for every package
func (c *Compiler) addPrelude(pkg *common.WhirlPackage, wf *common.WhirlFile) bool {
	corePath, _ := filepath.Abs(filepath.Join(c.whirlpath, "lib/std/core"))

	// conditionally perform the util prelude import (as necessary)
	if _, ok := wf.Annotations["no_util"]; !ok {
		// w/ empty slice since we want to select all imports
		if !c.preludeImport(pkg, wf, corePath, []string{}) {
			return false
		}
	}

	// perform the other mandatory prelude imports

	// TODO: update runtime import symbols list
	if !c.preludeImport(pkg, wf, filepath.Join(corePath, "runtime"), []string{"Strand", "Scheduler"}) {
		return false
	}

	// import everything from types
	if !c.preludeImport(pkg, wf, filepath.Join(corePath, "types"), []string{}) {
		return false
	}

	return true
}

// preludeImport performs a prelude import (only imports if necessary) for the given package
// of the package at the given path with the given list of symbols.  NOTE: if the slice is
// empty, prelude import assumes it should import everything.
func (c *Compiler) preludeImport(pkg *common.WhirlPackage, wf *common.WhirlFile, path string, symbols []string) bool {
	// paths are equal, nothing to import (redundant)
	if pkg.RootDirectory == path {
		return true
	}

	if ppkg, ok := c.importPackage(path); ok {
		// no need for redunancy check: should have already been checked by first condition

		// but, if the analysis is not done, then we have cyclic dependency with
		// the prelude; not good: this should never happen so if it does we need
		// to error
		if !ppkg.AnalysisDone {
			util.LogMod.LogFatal("Cyclic prelude dependency")
		}

		importedSymbols := make(map[string]*common.Symbol)
		if len(symbols) == 0 {
			// we should not need to do remote symbol checking: analysis done
			for name, sym := range ppkg.GlobalTable {
				importedSymbols[name] = sym.Import(false)
			}
		} else {
			for _, name := range symbols {
				if sym, ok := ppkg.GlobalTable[name]; ok {
					importedSymbols[name] = sym.Import(false)
				} else {
					util.LogMod.LogFatal(fmt.Sprintf("Import of nonexistent prelude symbol: `%s`", name))
				}
			}
		}

		// update the package's imports as necessary (since imports are always the same, we
		// only need to add a new blank list)
		if _, ok := pkg.ImportTable[ppkg.PackageID]; !ok {
			pkg.ImportTable[ppkg.PackageID] = &common.WhirlImport{
				PackageRef:      ppkg,
				ImportedSymbols: importedSymbols,
			}
		}

		// finally, import everything
		for name, sym := range importedSymbols {
			if _, ok := wf.LocalTable[name]; ok {
				util.LogMod.LogFatal(fmt.Sprintf("Duplicate prelude symbol import `%s`", name))
			} else {
				wf.LocalTable[name] = sym
			}
		}

		return true
	} else {
		util.LogMod.LogError(fmt.Errorf("Unable to properly import prelude (`%s`)", filepath.Base(path)))
		return false
	}
}
