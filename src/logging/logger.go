package logging

import "time"

// Logger is a type that is responsible for storing and logging output from the
// compiler as necessary
type Logger struct {
	ErrorCount int // Total encountered errors
	Warnings   []*LogMessage
	LogLevel   int

	// prevUpdate is used to hold the last time when the state updated
	prevUpdate time.Time
}

// Enumeration of the different log levels
const (
	LogLevelSilent  = iota // no output at all
	LogLevelError          // only errors and closing compilation notification (success/fail)
	LogLevelWarning        // errors, warnings, and closing message
	LogLevelVerbose        // errors, warnings, compiler version and progress summary, closing message (DEFAULT)
)

// LogMessage represents a thrown error or warning
type LogMessage struct {
	Message  string
	Kind     int
	Position *TextPosition
	Context  *LogContext
}

// Implementation of `error` for LogMessage (so it can be returned as one). This
// method should not be used to display a LogMessage.
func (lm *LogMessage) Error() string {
	return lm.Message
}

// Enumeration of the different kinds of a log messages
const (
	LMKToken  = iota // Error generating a token
	LMKSyntax        // Error parsing file
	LMKImport        // Error importing/lift-exporting package
	LMKTyping        // Error in type checking
	LMKMemory        // Error in memory analysis
	LMKImmut         // Error mutating an immutable value
	LMKUsage         // Error ocurring generally (due to some other rule)
	// TODO: add more as needed
)

// TextPosition represents a positional range in the source text
type TextPosition struct {
	StartLn, StartCol int // starting line, starting 0-indexed column
	EndLn, EndCol     int // ending Line, column trailing token (one over)
}

// LogContext represents the context in which an error occurred
type LogContext struct {
	PackageID uint
	FilePath  string
}

// CreateMessage is used to generate a log message in the given context
func (lctx *LogContext) CreateMessage(message string, kind int, pos *TextPosition) *LogMessage {
	return &LogMessage{
		Message:  message,
		Kind:     kind,
		Position: pos,
		Context:  lctx,
	}
}
