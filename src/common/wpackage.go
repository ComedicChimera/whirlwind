package common

import (
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/types"
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

	// LocalOperatorOverloads contains the signatures of all the operator
	// overloads only visible in this file (via. imports).  The values are the
	// operator overload signatures (since export statuses don't matter).
	LocalOperatorOverloads map[int][]types.DataType
}

// WhirlPackage represents a full, Whirlwind package (translation unit)
type WhirlPackage struct {
	// PackageID is a hash value of the package's path that is assigned to every
	// package to be placed before exported definitions to prevent name clashes
	// and is used as its entry the dependency graph and for import resolution
	PackageID uint

	// Name is the inferred name of the package based on its directory name
	Name string

	// RootDirectory is the directory the package's files are stored in (package
	// directory)
	RootDirectory string

	// Stores all of the files in a package
	Files map[string]*WhirlFile

	// Stores all of the globally-defined symbols in the package.
	GlobalTable map[string]*Symbol

	// Stores all of the overloaded operator definitions by their operator kind
	OperatorOverloads map[int][]*WhirlOperatorOverload

	// Stores all of the packages that this package imports (by ID) as well as
	// what items it imports (useful for constructing dependency graph)
	ImportTable map[string]*WhirlImport
}

// WhirlOperatorOverload represents an operator overload definition
type WhirlOperatorOverload struct {
	// Signature is the function type or generic type representing the
	// operator form (eg. `Integral + Integral => `Integral` = `func(Integral,
	// Integral)(Integral)`) defined for this operator overload.
	Signature types.DataType

	// Exported indicates whether or not the overload is exported
	Exported bool
}

// WhirlImport represents the collective imports of an entire package (so
// Whirlwind knows what declarations to generate in the output LLVM module)
type WhirlImport struct {
	PackageRef *WhirlPackage

	// All items that were actually used by the package.  (If value is `nil`,
	// then symbol hasn't be located yet -- and needs to be).
	ImportedSymbols map[string]*Symbol
}
