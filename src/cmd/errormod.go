package cmd

import (
	"fmt"
	"log"

	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// WhirlError respresents a positioned Whirlwind error (created by compiler)
type WhirlError struct {
	Message  string
	File     string
	Position *syntax.TextPosition
}

// NewWhirlError creates a new Whirlwind error from a message and text position
func NewWhirlError(message string, tp *syntax.TextPosition) *WhirlError {
	return &WhirlError{Message: message, Position: tp, File: C.CurrentFile}
}

func (we *WhirlError) Error() string {
	return fmt.Sprintf(we.Message+" at (Ln: %d, Col: %d) in %s\n", we.Position.StartLn, we.Position.StartCol, we.File)
}

// constants represent the different log levels the compiler can be set at None:
// only display anything if compilation was unsuccessful Fatal: display any
// errors and final compiler status message Warn: display any errors, warnings,
// and final compiler status message (default level) Verbose: display all
// information about compilation (inc. all prev levels)
const (
	LogLevelNone = iota
	LogLevelFatal
	LogLevelWarn
	LogLevelVerbose
)

// LogModule is a component of a compiler used to handle and display info about
// compilation.  Note that it does not change program flow: it is just a
// logistical component.  It should be notified of every error to ensure proper
// logging behavior as well as proper exit semantics
type LogModule struct {
	errors   []*WhirlError
	warnings []string
	loglevel int
}

// Display displays both the errors and the warnings on screen as well as the
// final compilation status.  It should be called before the compiler exits
// (whether or not compilation was successful - simple feedback mechanism)
func (lm *LogModule) Display() {

}

// CanProceed determines if any errors have been thrown and indicates to the
// caller whether or not the compiler should proceed into the next phase
func (lm *LogModule) CanProceed() bool {
	return len(lm.errors) == 0
}

// LogStateChange notifies the user of any major compiler state changes if they
// have set the log level to verbose (if not, does nothing)
func (lm *LogModule) LogStateChange(state string) {
	if lm.loglevel == LogLevelVerbose {

	}
}

// LogError logs an error with the error module.  Note that this function does
// not immediately cause an exit: it simply records the error so it can be
// properly displayed later so that errors can selectively bubble (in terms of
// compilation)
func (lm *LogModule) LogError(we *WhirlError) {
	lm.errors = append(lm.errors, we)
}

// LogWarning logs a warning message with the error module
func (lm *LogModule) LogWarning(wm string) {
	lm.warnings = append(lm.warnings, wm)
}

const fatalErrorMessage = `Uh oh! That wasn't supposed to happen.
Please open an issue on Github at https://github.com/ComedicChimera/Whirlwind
that contains the "bug" label along with some way to access to code you
tried to compile and the error message the compiler gave you. I am terribly
sorry for the inconvenience. I will fix this issue as soon as possible. This
language is an ongoing project and like all projects, it will have bugs.
Sorry again :(.`

// LogFatal logs a fatal compiler error.  Note that this is not an error in user
// code, but rather an error in the compiler itself.  Prompts the user to report
// the error on Github (open an issue - error)
func (lm *LogModule) LogFatal(m string) {
	fmt.Println("Fatal Compiler Error: " + m)
	fmt.Println(fatalErrorMessage)

	log.Fatalln("\nCompilation Failed.")

}
