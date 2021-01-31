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
	LocalTable map[string]*WhirlSymbolImport

	// LocalOperatorOverloads contains the signatures of all the operator
	// overloads only visible in this file (via. imports).  The values are the
	// operator overload signatures (since export statuses don't matter).
	LocalOperatorOverloads map[int][]typing.DataType

	// VisiblePackages lists all the packages that are visible by name or rename
	// in the current file.  The key is the name by with the package is visible.
	VisiblePackages map[string]*WhirlPackage

	// LocalBindings is a list of the bindings imported from other files that
	// are only available/visible in the current file.
	LocalBindings *typing.BindingRegistry
}

// WhirlSymbolImport represents a locally imported symbol in a file. The export
// status is implicit in the DeclStatus field of the symbol reference.
type WhirlSymbolImport struct {
	// SymbolRef is a reference to the symbol imported (shared).  The symbol
	// referenced will have no name until the symbol is resolved.  The
	// declaration status of this reference MUST NOT BE OVERIDDEN during
	// resolution.
	SymbolRef *Symbol

	// Position is the text position of the identifier for this symbol in its
	// explicit import.  This field is `nil` if this symbol was imported via a
	// namespace import.
	Position *logging.TextPosition

	// SrcPackage is the package this symbol was imported from
	SrcPackage *WhirlPackage
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

	// GlobalBindings stores all the interface bindings declared a global level
	// in the current package
	GlobalBindings *typing.BindingRegistry
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
	ImportedSymbols map[string]*Symbol
}

// WhirlOpaqueSymbol acts as a shared opaque symbol references during cyclic
// resolution.  One of these references should be created and distributed to all
// walkers in resolution unit.  Then, the contents of this reference should be
// updated as the opaque reference changes.
type WhirlOpaqueSymbol struct {
	Name string

	// This is used to determine whether or not this symbol should be visible in
	// the current package as well as whether or not when a definition is
	// complete if it should be updated by that finished definition (only if in
	// same package).
	SrcPackageID uint

	// Type can be any one of the opaque types
	Type typing.DataType
}
