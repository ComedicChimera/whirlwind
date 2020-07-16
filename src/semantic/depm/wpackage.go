package depm

import (
	"github.com/ComedicChimera/whirlwind/src/semantic"
	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// WhirlFile represents a single program file in a package
type WhirlFile struct {
	// Stores the root AST for the file (`file`)
	AST *syntax.ASTBranch

	// Root stores the HIR root of the file
	Root *semantic.HIRRoot

	// Stores all definitions local to this file.  No exported symbols should be
	// placed in this table
	LocalTable map[string]*semantic.Symbol

	// Stores all file-level annotations for the file (value is empty if the
	// annotation is just a flag)
	Annotations map[string]string
}

// WhirlPackage represents a full, Whirlwind package (translation unit)
type WhirlPackage struct {
	// PackageID is a randomly-generated string assigned to every package to be
	// placed before exported definitions to prevent name clashes and is used as
	// its entry the dependency graph and for import resolution
	PackageID string

	// Name is the inferred name of the package based on its directory name
	Name string

	// RootDirectory is the directory the package's files are stored in (package
	// directory)
	RootDirectory string

	// Stores all of the files in a package
	Files map[string]*WhirlFile

	// Stores all of the globally-defined symbols in the package.
	GlobalTable map[string]*semantic.Symbol

	// Stores all remote exports of this package (decl status = shared)
	RemoteExports map[string]*semantic.Symbol

	// Imports stores all of the packages that this package imports as well as
	// what items it imports (useful in building LLVM modules)
	ImportTable map[string]*WhirlImport
}

// WhirlImport represents the collective imports of an entire package (so
// Whirlwind knows what declarations to generate in the output LLVM module)
type WhirlImport struct {
	PackageRef *WhirlPackage

	// All items that were actually used by the package
	ImportedSymbols map[string]*semantic.Symbol
}
