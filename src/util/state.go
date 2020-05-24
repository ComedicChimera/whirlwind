package util

// THIS FILE IS USED TO STORE SHARED COMPILER STATE

// LogMod is a globally declared LogModule, it is used throughout the compiler
// to log and report compile error, warnings, and fatal errors as necessary
var LogMod *LogModule

// CurrentFile is the current file being compiled by the compiler
var CurrentFile string

// PointerSize is the size of a pointer on the target architecture.  Initialized
// by the compiler when it is created
var PointerSize uint
