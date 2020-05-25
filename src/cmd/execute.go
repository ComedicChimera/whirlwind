package cmd

import (
	"errors"
	"flag"
	"log"
	"os"
	"path"
	"runtime"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// WhirlPath is global path to the Whirlwind compiler directory (when lib is
// located)
var WhirlPath = os.Getenv("WHIRL_PATH")

// Execute should be called from main and initializes the compiler
func Execute() {
	// check for WHIRL_PATH (if it doesn't exist, we error out)
	if WhirlPath == "" {
		log.Fatal("Unable to locate WHIRL_PATH")
	}

	// ensure that a subcommand is passed to the compiler
	if len(os.Args) < 2 {
		log.Fatal("A valid subcommand is required")
	}

	// if any of these functions return some kind of error, we display it and
	// exit with status code 1, otherwise exit successfully
	var err error

	switch os.Args[1] {
	case "build":
		err = build()
	default:
		flag.PrintDefaults()
		os.Exit(1)
	}

	if err != nil {
		util.LogMod.LogFatal(err.Error())
	}
}

// Execute a `build` command
func build() error {
	// setup the build command and its flags
	buildCommand := flag.NewFlagSet("build", flag.ContinueOnError)

	buildCommand.String("p", runtime.GOOS, "Set the compilation platform")
	buildCommand.String("a", runtime.GOARCH, "Set the compilation architecture")
	buildCommand.String("f", "bin", "Set the output format { bin | obj | asm | llvm | lib | dll }")
	buildCommand.String("s", "", "List any static libraries that need to be linked with the binary")
	buildCommand.String("o", "", "Set the output file path")
	buildCommand.String("l", "", "Specify additional package directories")
	buildCommand.String("log", "warn", "Set compiler to display verbose output")
	buildCommand.String("dl", "", "List any dynamic libraries that need to be linked with the binary") // subject to change

	buildCommand.Bool("d", false, "Compile target in debug mode")

	// parse and check the command line arguments from the build command
	err := buildCommand.Parse(os.Args[2:])

	if err != nil {
		return err
	}

	if buildCommand.NArg() != 1 {
		return errors.New("Expecting exactly one argument which is the path to the build directory")
	}

	// collect all necessary information from the arguments
	buildDir := buildCommand.Arg(0)

	outputPath := buildCommand.Lookup("o").Value.String()
	if outputPath == "" {
		outputPath = path.Join(buildDir, "main")
	}

	// get the debug flag
	debugFlag := buildCommand.Lookup("d").Value.String() == "true"

	// try to create a compiler with that information
	compiler, err := NewCompiler(buildCommand.Lookup("p").Value.String(), buildCommand.Lookup("a").Value.String(), outputPath, buildDir, debugFlag)

	if err != nil {
		return err
	}

	// setup compiler state with any optional arguments the user specified (if
	// nothing given, assume sensible defaults)
	format := buildCommand.Lookup("f").Value.String()
	if format != "" {
		cerr := compiler.SetOutputFormat(format)

		if cerr != nil {
			return cerr
		}
	}

	localDirs := buildCommand.Lookup("l").Value.String()
	if localDirs != "" {
		cerr := compiler.AddLocalPackageDirectories(localDirs)

		if cerr != nil {
			return cerr
		}
	}

	staticLibs := buildCommand.Lookup("s").Value.String()
	if staticLibs != "" {
		cerr := compiler.AddStaticLibraries(staticLibs)

		if cerr != nil {
			return cerr
		}
	}

	// setup the global LogModule (based on log level)
	util.NewLogModule(buildCommand.Lookup("log").Value.String())

	// run the main compilation algorithm
	compiler.Compile()

	// the compiler will handle its own errors
	return nil
}
