package build

import (
	"fmt"
	"hash/fnv"
	"os"
	"path"
	"path/filepath"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// initPackage takes a directory path and parses all files in the directory and
// creates entries for them in a new package created based on the directory's
// name.  It does not extract any definitions or do anything more than
// initialize a package based on the contents and name of a provided directory.
// Note: User should check LogModule after this is called as file level errors
// are not returned!  `abspath` should be the absolute path to the package.
func (c *Compiler) initPackage(abspath string) (*common.WhirlPackage, error) {
	pkgName := path.Base(abspath)

	if !isValidPkgName(pkgName) {
		return nil, fmt.Errorf("Invalid package name: `%s`", pkgName)
	}

	pkg := &common.WhirlPackage{
		PackageID:     getPackageID(abspath),
		Name:          pkgName,
		RootDirectory: abspath,
		Files:         make(map[string]*common.WhirlFile),
	}

	// all file level errors are logged with the log module for display
	// later
	err := filepath.Walk(abspath, func(fpath string, info os.FileInfo, ferr error) error {
		if ferr != nil {
			util.LogMod.LogError(ferr)
		} else if info.IsDir() {
			return nil
		}

		if filepath.Ext(fpath) == util.SrcFileExtension {
			util.CurrentFile = fpath

			sc, err := syntax.NewScanner(fpath)

			if err != nil {
				util.LogMod.LogError(err)
			}

			ast, err := c.parser.Parse(sc)

			if err != nil {
				util.LogMod.LogError(err)
			}

			abranch := ast.(*syntax.ASTBranch)
			annots, shouldCompile := c.preprocessFile(abranch)

			if shouldCompile {
				pkg.Files[fpath] = &common.WhirlFile{AST: abranch, Annotations: annots}
			}
		}

		return nil
	})

	if err != nil {
		return nil, err
	}

	if len(pkg.Files) == 0 {
		return nil, fmt.Errorf("Unable to load package by name `%s` because it contains no source files", pkg.Name)
	}

	return pkg, nil
}

// isValidPkgName tests if the package name would be a usable identifier within
// Whirlwind. If it is not, the package name is considered to be invalid and an
// error should be thrown.
func isValidPkgName(pkgName string) bool {
	if syntax.IsLetter(rune(pkgName[0])) || pkgName[0] == '_' {
		for i := 1; i < len(pkgName); i++ {
			if !syntax.IsLetter(rune(pkgName[i])) && !syntax.IsDigit(rune(pkgName[i])) && pkgName[i] != '_' {
				return false
			}
		}

		return true
	}

	return false
}

// getPackageID calculates a package ID hash based on a package's file path
func getPackageID(abspath string) uint {
	h := fnv.New32a()
	h.Write([]byte(abspath))
	return uint(h.Sum32())
}
