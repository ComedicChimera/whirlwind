package build

import (
	"fmt"
	"math/rand"
	"os"
	"path"
	"path/filepath"
	"strings"
	"time"

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
		PackageID:     c.newPackageID(),
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
			if !strings.ContainsRune(pkgIDCharset, rune(pkgName[i])) && pkgName[i] != '_' {
				return false
			}
		}

		return true
	}

	return false
}

// PackageIDLen is a constant representing the length of a package ID string
const PackageIDLen = 8

// newPkgID generates a random package ID that does not already exist in the
// dependency graph (so as to avoid name clashes).
func (c *Compiler) newPackageID() string {
	var randName string

	for ok := true; ok; _, ok = c.depGraph[randName] {
		randName = newRandString(PackageIDLen)
	}

	return randName
}

// charset for package IDs
const pkgIDCharset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"

// seeded random number generator for random strings
var seededRand *rand.Rand = rand.New(
	rand.NewSource(time.Now().UnixNano()),
)

// newRandString returns a new random string of the given length based on the
// globally declared charset (pkgIDCharset).
func newRandString(length int) string {
	strbytes := make([]byte, length)
	charsetLen := len(pkgIDCharset)

	for i := range strbytes {
		strbytes[i] = pkgIDCharset[seededRand.Intn(charsetLen)]
	}

	return string(strbytes)
}
