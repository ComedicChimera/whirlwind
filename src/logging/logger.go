package logging

import "time"

// Logger is a type that is responsible for storing and logging output from the
// compiler as necessary
type Logger struct {
	ErrorCount int // Total encountered errors
	LogLevel   int

	// warnings is a list of all warnings to be logged at the end of compilation
	warnings []LogMessage

	// buildPath is used to shorten display paths in errors
	buildPath string

	// prevUpdate is used to hold the last time when the state updated
	prevUpdate time.Time

	// logMsgChan is the channel used for passing `LogMessages` to the Logger
	logMsgChan chan LogMessage
}

// Enumeration of the different log levels
const (
	LogLevelSilent  = iota // no output at all
	LogLevelError          // only errors and closing compilation notification (success/fail)
	LogLevelWarning        // errors, warnings, and closing message
	LogLevelVerbose        // errors, warnings, compiler version and progress summary, closing message (DEFAULT)
)

// newLogger creates a new logger struct
func newLogger(buildPath string, loglevel int) Logger {
	l := Logger{buildPath: buildPath, LogLevel: loglevel}

	l.logMsgChan = make(chan LogMessage)

	return l
}

// logLoop is run in a Goroutine to handle logging from all worker goroutines
// spawned by the compiler in the process of compilation.  It also means that
// logging is concurrent and non-blocking.  This isn't always necessary but for
// situations when many errors are being generated and logged concurrently, it
// means that the compiler isn't constantly slowing down to print and that one
// goroutine is handling all IO operations that aren't necessarily required for
// the compiler to function.  It also handles all updates to the Logger's state
func (l *Logger) logLoop() {
	for {
		if lm, ok := <-l.logMsgChan; ok {
			if lm.isError() {
				l.ErrorCount++

				if l.LogLevel > LogLevelSilent {
					lm.display()
				}
			} else {
				l.warnings = append(l.warnings, lm)
			}
		} else {
			break
		}
	}
}
