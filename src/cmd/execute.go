package cmd

import (
	"errors"
	"flag"
	"fmt"
	"os"
	"path"
	"path/filepath"
	"runtime"

	"whirlwind/build"
	"whirlwind/logging"
	"whirlwind/mods"
)

// Execute should be called from main and initializes the compiler
func Execute() {
	// WhirlPath is global path to the Whirlwind compiler directory (when lib is
	// located)
	whirlPath := os.Getenv("WHIRL_PATH")

	// check for WHIRL_PATH (if it doesn't exist, we error out)
	if whirlPath == "" {
		fmt.Println("Unable to locate WHIRL_PATH")
		fmt.Println("Go to https://whirlwind-lang.dev/install for more information.")
		os.Exit(1)
	}

	// ensure that a subcommand is passed to the compiler
	if len(os.Args) < 2 {
		printHelpMessage()
		os.Exit(1)
	}

	// if any of these functions return some kind of error, we display it and
	// exit with status code 1, otherwise exit successfully
	var err error

	switch os.Args[1] {
	case "build":
		err = Build(whirlPath)
	case "mod":
		err = Mod()
	case "version":
		fmt.Println("whirl v.0.1 - language version W.0.9")
	default:
		fmt.Printf("Unknown command `%s`\n\n", os.Args[1])
		printHelpMessage()
		os.Exit(1)
	}

	if err != nil {
		fmt.Println(err)
		os.Exit(1)
	}
}

// Build executes a `build` command. (`wp` = whirl path)
func Build(wp string) error {
	// setup the build command and its flags
	buildCommand := flag.NewFlagSet("build", flag.ContinueOnError)

	buildCommand.String("os", runtime.GOOS, "Set the target operating system")
	buildCommand.String("a", runtime.GOARCH, "Set the target architecture")
	buildCommand.String("f", "bin", "Set the output format { bin | obj | asm | llvm | lib | dll }")
	buildCommand.String("s", "", "List any static libraries that need to be linked with the binary")
	buildCommand.String("o", "", "Set the output file path")
	buildCommand.String("l", "", "Specify additional package directories")
	buildCommand.String("loglevel", "verbose", "Set compiler log level")
	buildCommand.String("dl", "", "List any dynamic libraries that need to be linked with the binary") // subject to change

	buildCommand.Bool("d", false, "Compile target in debug mode")
	buildCommand.Bool("forcegrebuild", false, "DEV OPTION: Force the compiler to rebuild grammar")

	// parse and check the command line arguments from the build command
	err := buildCommand.Parse(os.Args[2:])

	if err != nil {
		return err
	}

	if buildCommand.NArg() != 1 {
		return errors.New("The `build` command takes exactly one argument: the path to the build directory")
	}

	// collect all necessary information from the arguments
	buildDir := buildCommand.Arg(0)

	outputPath := buildCommand.Lookup("o").Value.String()
	if outputPath == "" {
		outputPath = path.Join(buildDir, "bin")
	}

	// get the debug flag
	debugFlag := buildCommand.Lookup("d").Value.String() == "true"

	// should never fail if the path exists relative to the working directory;
	// build directory needs to be an absolute path for imports to 100% work
	buildDir, _ = filepath.Abs(buildDir)

	// try to create a compiler with that information
	compiler, err := build.NewCompiler(buildCommand.Lookup("os").Value.String(),
		buildCommand.Lookup("a").Value.String(),
		outputPath, buildDir, debugFlag, wp,
	)

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

	// setup the global Logger (based on log level)
	logging.Initialize(buildDir, buildCommand.Lookup("loglevel").Value.String())

	// run the main compilation algorithm
	compiler.Compile(buildCommand.Lookup("forcegrebuild").Value.String() == "true")

	// the compiler will handle its own errors
	return nil
}

// Mod executes a `mod` command
func Mod() error {
	if len(os.Args) < 3 {
		fmt.Println("Missing subcommand")
		printModHelpMessage()
		return nil
	}

	switch os.Args[2] {
	case "del":
		finfo, err := os.Stat("whirl.mod")

		if err == nil {
			if finfo.IsDir() {
				return errors.New("`whirl.mod` must be a file")
			}

			return os.Remove("whirl.mod")
		} else if os.IsNotExist(err) {
			return errors.New("No module exists in the current directory")
		}

		return err
	case "new":
		if len(os.Args) != 4 {
			return errors.New("The `mod new` command takes exactly one argument: the name of the new module")
		}

		return mods.CreateModule(os.Args[3])
	case "init":
		if len(os.Args) != 4 {
			return errors.New("The `mod init` command takes exactly one argument: the name of the new module")
		}

		return mods.InitModule(os.Args[3], ".")
	case "rename":
	}

	return nil
}
