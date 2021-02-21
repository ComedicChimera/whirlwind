package logging

import (
	"bufio"
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"time"
)

// errorKindStringTable stores a table of all the error type strings organized
// by log message kind (eg. convert `LMKName` into "Name")
var errorKindStringTable = map[int]string{
	LMKImmut:    "Mutability",
	LMKImport:   "Import",
	LMKMemory:   "Memory",
	LMKName:     "Name",
	LMKSyntax:   "Syntax",
	LMKToken:    "Token",
	LMKTyping:   "Type",
	LMKUsage:    "Usage",
	LMKMetadata: "Metadata",
	LMKUser:     "User",
	LMKInterf:   "Interface",
}

// displayLogMessage displays a LogMessage.  isError is used to determine the
// header for the error (eg. "Type Error"  or "Type Warning").
func displayLogMessage(buildPath string, lm *LogMessage, isError bool) {
	displayLogMessageHeader(buildPath, lm, isError)

	if lm.Position == nil {
		fmt.Println(lm.Message)
		return
	}

	fmt.Printf("%s at (Ln: %d, Col: %d)\n\n", lm.Message, lm.Position.StartLn, lm.Position.StartCol+1)

	// `f` should be guaranteed to exist since it was opened earlier (unless the
	// user deleted the file in between running the compiler and this function
	// being called which should cause a panic -- not a compiler error)
	f, _ := os.Open(lm.Context.FilePath)
	defer f.Close()

	sc := bufio.NewScanner(f)

	// we need to make sure the line is scanned in and ready to go; we can
	// assume the line number is correct (since it was determined by our scanner
	// before this function was called -- if it isn't, panic)
	for i := 0; sc.Scan() && i < lm.Position.StartLn-1; i++ {
	}

	displayCodeSelection(sc, lm.Position)
}

// displayCodeSelection displays the code that an error occurs on and highlights
// the relevant lines.  This function takes a scanner that should be positioned
// at the starting line for that code (calling scanner.Text() yields that line).
func displayCodeSelection(sc *bufio.Scanner, pos *TextPosition) {
	minLnNumberLen := len(strconv.Itoa(pos.EndLn))
	lnNumberFmtStr := "%-" + strconv.Itoa(minLnNumberLen) + "v | "

	for line := pos.StartLn; line <= pos.EndLn; line++ {
		fmt.Printf(lnNumberFmtStr, line)

		// convert all tabs to four spaces (for consistency)
		fmt.Println(strings.ReplaceAll(sc.Text(), "\t", "    "))

		fmt.Print(strings.Repeat(" ", minLnNumberLen+3))

		if pos.StartLn == pos.EndLn {
			fmt.Print(strings.Repeat(" ", pos.StartCol))
			fmt.Println(strings.Repeat("^", pos.EndCol-pos.StartCol))
		} else {
			if line == pos.StartLn {
				fmt.Print(strings.Repeat(" ", pos.StartCol))
				fmt.Println(strings.Repeat("^", len(sc.Text())-pos.StartCol))
			} else if line == pos.EndLn {
				fmt.Println(strings.Repeat("^", pos.EndCol+1))
			} else {
				fmt.Println(strings.Repeat("^", len(sc.Text())))
			}

			sc.Scan()
		}
	}
}

// displayLogMessageHeader displays the header/banner that is placed on top of
// every formal log message (contains error kind, filepath, etc)
func displayLogMessageHeader(buildPath string, lm *LogMessage, isError bool) {
	fmt.Print("\n\n--- ")

	sb := strings.Builder{}
	sb.WriteString(errorKindStringTable[lm.Kind])

	if isError {
		sb.WriteString(" Error ")
	} else {
		sb.WriteString(" Warning ")
	}

	sb.WriteString(strings.Repeat("-", 28-sb.Len()))
	fmt.Print(sb.String())

	fpath, _ := filepath.Rel(buildPath, lm.Context.FilePath)

	// anything that is not in the build directory may be in the library
	// directory so we can check for that (simplify our paths)
	if strings.HasPrefix(fpath, "..") {
		fpath, _ = filepath.Rel(filepath.Join(os.Getenv("WHIRL_PATH"), "lib"), lm.Context.FilePath)

		// if it also not in our library directory, then we just use the abspath
		if strings.HasPrefix(fpath, "..") {
			fpath = lm.Context.FilePath
		}
	}

	fmt.Printf(" (file: %s)\n", filepath.Clean(fpath))
}

// displayStdError displays a standard Go error that is not a LogMessage
func displayStdError(err error) {
	fmt.Printf("\n\n%s\n", err)
}

// fatalErrorMessage is the string printed before after any fatal error
const fatalErrorMessage = `Uh oh! That wasn't supposed to happen.
Please open an issue on Github at https://github.com/ComedicChimera/Whirlwind
that contains the "bug" label along with some way to access to code you
tried to compile and the error message the compiler gave you. I am terribly
sorry for the inconvenience. I will fix this issue as soon as possible. This
language is an ongoing project and like all projects, it will have bugs.
Sorry again :(`

// displayFatalMessage displays a fatal error message (DOES NOT EXIT)
func displayFatalMessage(message string) {
	fmt.Printf("\n\nUnexpected Fatal Error: %s\n", message)
	fmt.Println(fatalErrorMessage)
}

// MaxStateLength is the number of character required to represent a state
// change (len("Compiling LLVM") == 14).  Used by Log Module.
const MaxStateLength = 14

// displayCompilationInfo displays the "start-up" information about the compiler
// and the current compilation process (target, version, debug, etc.)
func displayCompilationInfo(targetOS, targetArch string, debug bool) {
	fmt.Println("Jasmine (whirlc) v.0.1 - LV: W.0.9")

	if debug {
		fmt.Printf("Target: %s/%s (debug)\n", targetOS, targetArch)
	} else {
		fmt.Printf("Target: %s/%s (release)\n", targetOS, targetArch)
	}

	fmt.Println("Compiling:")
}

// displayStateChange shows a state change message
func displayStateChange(prevUpdate time.Time, newstate string) {
	// only update if prevUpdate is not `nil` has already been set
	if !prevUpdate.IsZero() {
		fmt.Printf("Done (%f.3s)\n", time.Since(prevUpdate).Seconds())
	}

	// pad the state string out with dots
	stateString := newstate + strings.Repeat(".", MaxStateLength-len(newstate)+3)

	// no newline so that the `Done (...)` will print on the same line
	fmt.Printf("\t%s ", stateString)
}

// displayFinalMessage displays the conclusive message for compilation
func displayFinalMessage(errorCount, warningCount int) {
	if errorCount == 0 {
		fmt.Printf("\n\nCompilation Succeeded (0 errors, %d warnings)\n", warningCount)
	} else {
		fmt.Printf("\n\nCompilation Failed (%d errors, %d warnings)\n", errorCount, warningCount)
	}
}
