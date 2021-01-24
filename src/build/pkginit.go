package build

import (
	"fmt"
	"hash/fnv"
	"os"
	"path"
	"path/filepath"
	"strings"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/logging"
	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// SrcFileExtension is used to indicate what the file extension is for a
// Whirlwind source file (used to identify files when loading packages)
const SrcFileExtension = ".wrl"

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
			logging.LogStdError(ferr)
		} else if info.IsDir() {
			return nil
		}

		if filepath.Ext(fpath) == SrcFileExtension {
			c.lctx.FilePath = fpath

			sc, err := syntax.NewScanner(fpath, c.lctx)

			if err != nil {
				logging.LogStdError(err)
				return nil
			}

			shouldCompile, err := c.preprocessFile(sc)
			if err != nil {
				logging.LogStdError(err)
				return nil
			}

			if !shouldCompile {
				return nil
			}

			ast, err := c.parser.Parse(sc)

			if err != nil {
				logging.LogStdError(err)
				return nil
			}

			abranch := ast.(*syntax.ASTBranch)
			pkg.Files[fpath] = &common.WhirlFile{AST: abranch, Annotations: c.collectAnnotations(abranch)}
		}

		return nil
	})

	if err != nil {
		return nil, err
	}

	if len(pkg.Files) == 0 {
		return nil, fmt.Errorf("Unable to load package by name `%s` because it contains no source files", pkg.Name)
	}

	c.depGraph[pkg.PackageID] = pkg
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

// collectAnnotations checks for and walks annotations at the top of an already
// parsed file.  It returns a map of the annotations it collects as well as a
// boolean indicating whether or not the current build configuration indicates
// that the file should be compiled (ie. accounts for annotations like `#arch`)
func (c *Compiler) collectAnnotations(fileAST *syntax.ASTBranch) map[string]string {
	annotations := make(map[string]string)

loop:
	for _, node := range fileAST.Content {
		branch := node.(*syntax.ASTBranch)

		switch branch.Name {
		case "annotation":
			if len(branch.Content) == 2 {
				annotations[branch.Content[1].(*syntax.ASTLeaf).Value] = ""
			} else {
				annotations[branch.Content[1].(*syntax.ASTLeaf).Value] =
					strings.Trim(branch.Content[2].(*syntax.ASTLeaf).Value, "\"")
			}
		case "exported_import", "import_stmt", "top_level":
			break loop
		}
	}

	return annotations
}
