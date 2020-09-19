package common

import (
	"github.com/ComedicChimera/whirlwind/src/logging"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/typing"
)

// WhirlFile represents a single program file in a package
type WhirlFile struct {
	// Stores the root AST for the file (`file`)
	AST *syntax.ASTBranch

	// Stores all file-level annotations for the file (value is empty if the
	// annotation is just a flag)
	Annotations map[string]string

	// Root stores the HIR root of the file
	Root *HIRRoot

	// LocalTable stores all of the symbols imported in the current file
	// that exist at the top level (ie. globally) but are not visible in
	// other files in the same package (used to facilitate imports)
	LocalTable map[string]*Symbol

	// LocalOperatorOverloads contains the signatures of all the operator
	// overloads only visible in this file (via. imports).  The values are the
	// operator overload signatures (since export statuses don't matter).
	LocalOperatorOverloads map[int][]typing.DataType

	// VisiblePackages lists all the packages that are visible by name or rename
	// in the current file.  The key is the name by with the package is visible.
	// Full namespace imports should also be stored here (using some form of
	// unique suffix) -- namespace import status should be determined by
	// confirming it with package's import entry in the import table (which
	// should list the files that import its namespace)
	VisiblePackages map[string]*WhirlPackage
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
	ImportTable map[uint]*WhirlImport
}

// WhirlOperatorOverload represents an operator overload definition
type WhirlOperatorOverload struct {
	// Signature is the function type or generic type representing the
	// operator form (eg. `Integral + Integral => `Integral` = `func(Integral,
	// Integral)(Integral)`) defined for this operator overload.
	Signature typing.DataType

	// Exported indicates whether or not the overload is exported
	Exported bool
}

// WhirlImport represents the collective imports of an entire package (so
// Whirlwind knows what declarations to generate in the output LLVM module)
type WhirlImport struct {
	PackageRef *WhirlPackage

	// All of the symbols imported/used in the current package.  This field is
	// meaningless and therefore can be ignored during a namespace import.
	ImportedSymbols map[string]*WhirlSymbolImport

	// NamespaceImports is a map of all of the files that import the package's
	// namespace (ie. use the `...` import notation).  This is a map for fast
	// look-ups.
	NamespaceImports map[*WhirlFile]struct{}
}

// WhirlSymbolImport represents an imported symbol (with reference and position)
type WhirlSymbolImport struct {
	// Name of the imported symbol
	Name string

	// SymbolRef is a reference to the symbol imported
	SymbolRef *Symbol

	// Positions is a list of all places where this symbol is imported
	Positions []*logging.TextPosition
}
