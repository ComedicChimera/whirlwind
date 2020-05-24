package cmd

import (
	"errors"
	"os"
	"strings"

	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// Store the different possible output formats for the compiler
const (
	BIN = iota
	LLVM
	ASM
	OBJ
	DLL
	LIB
)

// Compiler is a singleton struct meant to store all compiler state for
// continuous use: main mechanism of compilation
type Compiler struct {
	// build configurations
	platform            string
	architecture        string
	localPkgDirectories []string
	staticLibraries     []string
	outputPath          string
	buildDirectory      string
	outputFormat        int

	// compiler state
	parser *syntax.Parser
}

// AddLocalPackageDirectories interprets a command-line input string for the
// local directories argument (extracts the directories from it if possible) and
// stores it as compiler state. it also checks if the directories exist (if they
// don't, it returns an error)
func (c *Compiler) AddLocalPackageDirectories(directories string) error {
	c.localPkgDirectories = strings.Split(directories, ",")

	for _, dir := range c.localPkgDirectories {
		if _, err := os.Stat(dir); os.IsNotExist(err) {
			return err
		}
	}

	return nil
}

// AddStaticLibraries interprets the command-line input string for the static
// libraries argument: extracts all possible library paths, checks if they
// exist, and adds them returns an error if it fails to do any of the steps in
// adding said libs (doesn't check whether or not the libs are actually usable,
// that comes later :D)
func (c *Compiler) AddStaticLibraries(libraryPaths string) error {
	c.staticLibraries = strings.Split(libraryPaths, ",")

	for _, file := range c.staticLibraries {
		if _, err := os.Stat(file); os.IsNotExist(err) {
			return err
		}
	}

	return nil
}

// SetOutputFormat converts the command-line format name into a usable format
// specifier if possible returns an error if its unable to do so (see list above
// for valid output format types)
func (c *Compiler) SetOutputFormat(formatName string) error {
	switch formatName {
	case "bin":
		c.outputFormat = BIN
	case "llvm":
		c.outputFormat = LLVM
	case "asm":
		c.outputFormat = ASM
	case "obj":
		c.outputFormat = OBJ
	case "dll":
		c.outputFormat = DLL
	case "lib":
		c.outputFormat = LIB
	default:
		return errors.New("Invalid output format")
	}

	return nil
}

// NewCompiler creates a new, singletone compiler based on the essential input
// information (p: platform, a: architecture, op: output path, bd: build
// directory). It then stores the compiler globally if its creation was
// successful
func NewCompiler(p string, a string, op string, bd string) (*Compiler, error) {
	switch p {
	case "windows", "osx", "ubuntu", "debian", "freebsd":
		break
	default:
		return nil, errors.New("Unsupported platform")
	}

	if a != "x86" && a != "x64" {
		return nil, errors.New("Unsupported architecture")
	}

	if _, err := os.Stat(bd); os.IsNotExist(err) {
		return nil, errors.New("Build directory does not exist")
	}

	return &Compiler{platform: p, architecture: a, outputPath: op, buildDirectory: bd}, nil
}

// Compile runs the main compilation algorithm: it returns no value and does all
// necessary creation and error handling (program should simply exit after this
// returns)
func (c *Compiler) Compile() {
	// initialize any necessary globals
	c.setPointerSize()
}

// determines the pointer size for any given architecture (and/or platform)
// TODO: confirm that these sizes will work as a general rule
func (c *Compiler) setPointerSize() {
	switch c.architecture {
	case "x86":
		util.PointerSize = 4
	case "x64":
		util.PointerSize = 8
	}
}
