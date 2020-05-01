package cmd

import (
	"errors"
	"os"
	"strings"
)

const (
	BIN = iota
	LLVM
	ASM
	OBJ
	DLL
	LIB
)

type Compiler struct {
	// build configurations
	platform            string
	architecture        string
	localPkgDirectories []string
	staticLibraries     []string
	outputPath          string
	buildDirectory      string
	outputFormat        int
}

func (c *Compiler) AddLocalPackageDirectories(directories string) error {
	c.localPkgDirectories = strings.Split(directories, ",")

	for _, dir := range c.localPkgDirectories {
		if _, err := os.Stat(dir); os.IsNotExist(err) {
			return err
		}
	}

	return nil
}

func (c *Compiler) AddStaticLibraries(libraryPaths string) error {
	c.staticLibraries = strings.Split(libraryPaths, ",")

	for _, file := range c.staticLibraries {
		if _, err := os.Stat(file); os.IsNotExist(err) {
			return err
		}
	}

	return nil
}

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

	if _, err := os.Stat(op); os.IsNotExist(err) {
		return nil, errors.New("Output directory does not exist")
	}

	if _, err := os.Stat(bd); os.IsNotExist(err) {
		return nil, errors.New("Build directory does not exist")
	}

	c := &Compiler{platform: p, architecture: a, outputPath: op, buildDirectory: bd}

	return c, nil
}
