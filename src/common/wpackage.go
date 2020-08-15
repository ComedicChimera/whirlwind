package common

import (
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// WhirlFile represents a single program file in a package
type WhirlFile struct {
	// Stores the root AST for the file (`file`)
	AST *syntax.ASTBranch

	// Root stores the HIR root of the file
	Root *HIRRoot

	// LocalTable stores all of the symbols imported in the current file
	// that exist at the top level (ie. globally) but are not visible in
	// other files in the same package (used to facilitate imports)
	LocalTable map[string]*Symbol

	// Stores all file-level annotations for the file (value is empty if the
	// annotation is just a flag)
	Annotations map[string]string

	// VisiblePackages is a list of all of the packages whose names are visible
	// in this specific file (faciliates package importing).  The key is the
	// name by which the package is accessible in the current package.
	VisiblePackages map[string]*WhirlPackage
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
	GlobalTable map[string]*Symbol

	// Stores all of the symbols that other packages depend on in this package
	// that have not been resolved (ie. for handling cyclic imports)
	RemoteSymbols map[string]*RemoteSymbol

	// Stores all of the packages that this package imports (by ID) as well as
	// what items it imports (useful in building LLVM modules)
	ImportTable map[string]*WhirlImport

	// Stores all of the overloaded operator definitions
	OperatorOverloads map[string]types.DataType

	// AnalysisDone is a flag indicating whether or not the package has been
	// fully analyzed yet.  It is used to test for import cycles.
	AnalysisDone bool
}

// WhirlImport represents the collective imports of an entire package (so
// Whirlwind knows what declarations to generate in the output LLVM module)
type WhirlImport struct {
	PackageRef *WhirlPackage

	// All items that were actually used by the package
	ImportedSymbols map[string]*Symbol
}

// RemoteSymbol represents a symbol that was requested/imported by another
// package that has yet to be resolved.  These symbols include a shared symbol
// reference (for all users of the remote symbol) as well as a reference to the
// first position this remote symbol was requested in.
type RemoteSymbol struct {
	SymRef   *Symbol
	Position *util.TextPosition
}
