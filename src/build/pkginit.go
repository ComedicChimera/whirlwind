package build

import (
	"fmt"
	"hash/fnv"
	"io/ioutil"
	"path/filepath"

	"whirlwind/common"
	"whirlwind/logging"
	"whirlwind/mods"
	"whirlwind/syntax"
	"whirlwind/typing"
)

// SrcFileExtension is used to indicate what the file extension is for a
// Whirlwind source file (used to identify files when loading packages)
const SrcFileExtension = ".wrl"

// initPackage takes a directory path and parses all files in the directory and
// creates entries for them in a new package created based on the directory's
// name.  It does not extract any definitions or do anything more than
// initialize a package based on the contents and name of a provided directory.
// The `parentModule` is the default module to be passed to newly initialized
// package if no other module is found.  Note that this parent module will be
// superceded by any module found in the local directory of the package being
// initialized.  Importantly, caller should check LogModule after this is called
// as file level errors are not returned!  `abspath` should be the absolute path
// to the package. It also returns a boolean flag indicating whether or not
// compilation should proceed.
func (c *Compiler) initPackage(abspath string, parentModule *mods.Module) (*common.WhirlPackage, bool) {
	pkgName := filepath.Base(abspath)

	// check if the package name is valid
	if !mods.IsValidPackageName(pkgName) {
		logging.LogInternalError("Package", fmt.Sprintf("Invalid package name: `%s`", pkgName))
		return nil, false
	}

	// attempt to load the module if one exists or just take the parent module
	mod, ok := mods.LoadModule(abspath)
	if !ok {
		mod = parentModule
	}

	// initialize the package struct itself
	pkg := &common.WhirlPackage{
		PackageID:         getPackageID(abspath),
		Name:              pkgName,
		RootDirectory:     abspath,
		Files:             make(map[string]*common.WhirlFile),
		ImportTable:       make(map[uint]*common.WhirlImport),
		OperatorOverloads: make(map[int][]*common.WhirlOperatorOverload),
		GlobalTable:       make(map[string]*common.Symbol),
		GlobalBindings:    &typing.BindingRegistry{},
		ParentModule:      mod,
	}

	// try to open the package directory
	files, err := ioutil.ReadDir(abspath)
	if err != nil {
		logging.LogInternalError("File", err.Error())
	}

	// walk each file in the directory.  all file level errors (eg. syntax
	// errors) are logged with the log module for display later -- this is
	// to ensure that every file is walked at least once
	for _, fInfo := range files {
		if !fInfo.IsDir() && filepath.Ext(fInfo.Name()) == SrcFileExtension {
			fpath := filepath.Join(abspath, fInfo.Name())
			c.lctx.FilePath = fpath

			sc, ok := syntax.NewScanner(fpath, c.lctx)

			if !ok {
				continue
			}

			tags, shouldCompile := c.preprocessFile(sc)
			if !shouldCompile {
				continue
			}

			ast, ok := c.parser.Parse(sc)

			if !ok {
				continue
			}

			abranch := ast.(*syntax.ASTBranch)
			pkg.Files[fpath] = &common.WhirlFile{
				AST:                    abranch,
				MetadataTags:           tags,
				LocalTable:             make(map[string]*common.WhirlSymbolImport),
				LocalOperatorOverloads: make(map[int][]typing.DataType),
				VisiblePackages:        make(map[string]*common.WhirlPackage),
				LocalBindings:          &typing.BindingRegistry{},
			}
		}
	}

	if len(pkg.Files) == 0 {
		logging.LogInternalError("Package", fmt.Sprintf("Unable to load package by name `%s` because it contains no source files", pkg.Name))
		return nil, false
	}

	c.depGraph[pkg.PackageID] = pkg
	return pkg, logging.ShouldProceed()
}

// getPackageID calculates a package ID hash based on a package's file path
func getPackageID(abspath string) uint {
	h := fnv.New32a()
	h.Write([]byte(abspath))
	return uint(h.Sum32())
}
