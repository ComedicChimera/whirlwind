package util

import (
	"fmt"
	"log"
	"strings"
	"time"
)

// MaxStateLength is the number of character required to represent a state
// change (len("Compiling LLVM") == 14).  Used by Log Module.
const MaxStateLength = 14

// TextPosition represents the positional range of an AST node in the source
// text (for error handling)
type TextPosition struct {
	StartLn, StartCol int // starting line, starting 0-indexed column
	EndLn, EndCol     int // ending Line, column trailing token (one over)
}

// WhirlError respresents a positioned Whirlwind error (created by compiler)
type WhirlError struct {
	Message  string
	File     string
	Position *TextPosition

	// indicates what type of error was thrown: used in display (--- $Kind Error ----)
	Kind string

	// optional filed indicating that there is a suggested correction for the
	// error. if this field is empty (""), then no suggestion is displayed (no
	// suggestion exists)
	Suggestion string
}

// NewWhirlError creates a new Whirlwind error from a message and text position
func NewWhirlError(message, errorKind string, tp *TextPosition) *WhirlError {
	return &WhirlError{Message: message, Kind: errorKind, Position: tp, File: CurrentFile}
}

// NewWhirlErrorWithSuggestion creates a new Whirlwind error with a suggested
// correction. It has the same behavior as the first function except it also
// populates the Suggestion field
func NewWhirlErrorWithSuggestion(message, errorKind, suggestion string, tp *TextPosition) *WhirlError {
	return &WhirlError{Message: message, Kind: errorKind, Suggestion: suggestion, Position: tp, File: CurrentFile}
}

// Error does not produce the full error message: it simply produces the top
// line ie. the error details (message, line, and column)
func (we *WhirlError) Error() string {
	return we.Message + fmt.Sprintf(" at (Ln: %d, Col: %d)\n", we.Position.StartLn, we.Position.StartCol)
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
	errors   []error
	warnings []string
	loglevel int

	// prevUpdate is used to hold the last time when the state updated
	prevUpdate time.Time
}

// NewLogModule creates a new global log module based on the given loglevel
// string if possible.
func NewLogModule(loglevelstr string) bool {
	LogMod = &LogModule{}

	switch loglevelstr {
	case "warn":
		LogMod.loglevel = LogLevelWarn
	case "fatal":
		LogMod.loglevel = LogLevelFatal
	case "none":
		LogMod.loglevel = LogLevelNone
	case "verbose":
		LogMod.loglevel = LogLevelVerbose
	default:
		return false
	}

	return true
}

// ShowInfo displays basic compiler information if the log level is verbose.
// `o` = target OS, `a` = target architecture
func (lm *LogModule) ShowInfo(o, a string, debugMode bool) {
	if lm.loglevel == LogLevelVerbose {
		fmt.Println("Jasmine (whirlc) v.0.1 - LV: W.0.9")

		if debugMode {
			fmt.Printf("Target: %s/%s (debug)\n", o, a)
		} else {
			fmt.Printf("Target: %s/%s (release)\n", o, a)
		}

		fmt.Println("Compiling:")

		// initial prevUpdate time is after ShowInfo is called
		lm.prevUpdate = time.Now()
	}
}

// ShowStateFinish notifies the user of any major compiler state changes if they
// have set the log level to verbose (ie. when a state ends, times stages too)
func (lm *LogModule) ShowStateFinish(state string) {
	if lm.loglevel == LogLevelVerbose {
		// pad the state string out with dots
		stateString := state + strings.Repeat(".", MaxStateLength-len(state)+3)

		// timing is not perfect but it is accurate enough for user feedback
		fmt.Printf("\t%s (%f.3s)\n", stateString, time.Since(lm.prevUpdate).Seconds())
		lm.prevUpdate = time.Now()
	}
}

// ShowStatus displays both the errors and the warnings on screen as well as the
// final compilation status.  It should be called before the compiler exits
// (whether or not compilation was successful - simple feedback mechanism).
func (lm *LogModule) ShowStatus() {

}

// CanProceed determines if any errors have been thrown and indicates to the
// caller whether or not the compiler should proceed into the next phase
func (lm *LogModule) CanProceed() bool {
	return len(lm.errors) == 0
}

// LogError logs an error with the error module.  Note that this function does
// not immediately cause an exit: it simply records the error so it can be
// properly displayed later so that errors can selectively bubble (in terms of
// compilation)
func (lm *LogModule) LogError(err error) {
	lm.errors = append(lm.errors, err)
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
