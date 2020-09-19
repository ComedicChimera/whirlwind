package logging

import (
	"fmt"
	"strings"
	"time"
)

// displayLogMessage displays a LogMessage.  isError is used to determine the
// header for the error (eg. "Type Error"  or "Type Warning").
func displayLogMessage(lm *LogMessage, isError bool) {

}

// fatalErrorMessage is the string printed before after any fatal error
const fatalErrorMessage = `Uh oh! That wasn't supposed to happen.
Please open an issue on Github at https://github.com/ComedicChimera/Whirlwind
that contains the "bug" label along with some way to access to code you
tried to compile and the error message the compiler gave you. I am terribly
sorry for the inconvenience. I will fix this issue as soon as possible. This
language is an ongoing project and like all projects, it will have bugs.
Sorry again :(.`

// displayFatalMessage displays a fatal error message (DOES NOT EXIT)
func displayFatalMessage(message string) {
	fmt.Printf("Unexpected Fatal Error: %s\n\n", message)
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
		fmt.Printf("Compilation Succeeded (0 errors, %d warnings)\n", warningCount)
	} else {
		fmt.Printf("Compilation Failed (%d errors, %d warnings)\n", errorCount, warningCount)
	}
}
