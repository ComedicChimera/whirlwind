package cmd

import (
	"errors"
	"flag"
	"log"
	"os"
	"path"
	"runtime"
)

var WHIRL_PATH = os.Getenv("WHIRL_PATH")

func Execute() {
	// check for WHIRL_PATH (if it doesn't exist, we error out)
	if WHIRL_PATH == "" {
		log.Fatal("Unable to locate WHIRL_PATH")
	}

	// ensure that a subcommand is passed to the compiler
	if len(os.Args) < 2 {
		log.Fatal("A valid subcommand is required")
	}

	// if any of these functions return some kind of error, we display it
	// and exit with status code 1, otherwise exit successfully
	var err error

	switch os.Args[1] {
	case "build":
		err = build()
	default:
		flag.PrintDefaults()
		os.Exit(1)
	}

	if err != nil {
		log.Fatal(err)
	}
}

func build() error {
	// setup the build command and its flags
	buildCommand := flag.NewFlagSet("build", flag.ContinueOnError)

	buildCommand.String("p", runtime.GOOS, "Set the compilation platform")
	buildCommand.String("a", runtime.GOARCH, "Set the compilation architecture")
	buildCommand.String("f", "bin", "Set the output format { bin | obj | asm | llvm | lib | dll }")
	buildCommand.String("s", "", "List any static libraries that need to be linked with the binary")
	buildCommand.String("o", "", "Set the output file path")
	buildCommand.String("l", "", "Specify additional package directories")
	buildCommand.Bool("v", false, "Set compiler to display verbose output")

	err := buildCommand.Parse(os.Args[2:])

	if err != nil {
		return err
	}

	if buildCommand.NArg() != 1 {
		return errors.New("Expecting exactly one argument which is the path to the build directory")
	}

	buildDir := buildCommand.Arg(0)

	outputPath := buildCommand.Lookup("o").Value.String()
	if outputPath == "" {
		outputPath = path.Join(buildDir, "main")
	}

	c, err := NewCompiler(buildCommand.Lookup("p").Value.String(), buildCommand.Lookup("a").Value.String(), outputPath, buildDir)

	if err != nil {
		return err
	}

	format := buildCommand.Lookup("f").Value.String()
	if format != "" {
		cerr := c.SetOutputFormat(format)

		if cerr != nil {
			return cerr
		}
	}

	localDirs := buildCommand.Lookup("l").Value.String()
	if localDirs != "" {
		cerr := c.AddLocalPackageDirectories(localDirs)

		if cerr != nil {
			return cerr
		}
	}

	staticLibs := buildCommand.Lookup("s").Value.String()
	if staticLibs != "" {
		cerr := c.AddStaticLibraries(staticLibs)

		if cerr != nil {
			return cerr
		}
	}

	return nil
}
