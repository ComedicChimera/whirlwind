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
	VisiblePackages map[string]*WhirlPackage

	// NamespaceImports lists the packages whose exported namespaces are
	// imported by the current file (this is factored into the package level
	// WhirlImport as well).  The value indicates whether or not the namespace
	// should be exported or not.
	NamespaceImports map[*WhirlPackage]bool
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

	// Files stores all of the files in a package
	Files map[string]*WhirlFile

	// GlobalTable stores all of the globally-defined symbols in the package.
	GlobalTable map[string]*Symbol

	// OperatorOverloads stores all of the overloaded operator definitions by
	// their operator kind
	OperatorOverloads map[int][]*WhirlOperatorOverload

	// ImportTable Stores all of the packages that this package imports (by ID)
	// as well as what items it imports (useful for constructing dependency
	// graph)
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
	// meaningless and therefore can be ignored during a namespace import.  The
	// key is the name of the symbol (which may not be given in the SymbolRef).
	ImportedSymbols map[string]*WhirlSymbolImport

	// NamespaceImport indicates whether the imported package's exported
	// namespace is used in its entirety by a namespace import in this package.
	NamespaceImport bool
}

// WhirlSymbolImport represents an imported symbol (with reference and position)
type WhirlSymbolImport struct {
	// SymbolRef is a reference to the symbol imported
	SymbolRef *Symbol

	// Positions is a list of all places where this symbol is imported
	Positions map[*WhirlFile]*logging.TextPosition
}
