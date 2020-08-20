package util

// THIS FILE IS USED TO STORE SHARED COMPILER STATE

// LogMod is a globally declared LogModule, it is used throughout the compiler
// to log and report compile error, warnings, and fatal errors as necessary
var LogMod *LogModule

// CurrentFile is the path to the current file being compiled by the compiler
var CurrentFile string

// CurrentPackageID is the ID of the current package being compiled. This is not
// meant for display: facilitates global bindings, etc.
var CurrentPackageID string

// // CurrentPackagePath is absolute path to the current package
// var CurrentPackagePath string

// PointerSize is the size of a pointer on the target architecture.  Initialized
// by the compiler when it is created
var PointerSize uint

// SrcFileExtension is used to indicate what the file extension is for a
// Whirlwind source file
const SrcFileExtension = ".wrl"
