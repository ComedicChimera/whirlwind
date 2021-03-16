package logging

import (
	"os"
	"time"
)

// logger is a global reference to a shared Logger (created/initialized with the
// compiler, but separated for general usage)
var logger Logger

// Initialize initializes the global logger with the provided log level
func Initialize(buildPath string, loglevelname string) {
	var loglevel int
	switch loglevelname {
	case "silent":
		loglevel = LogLevelSilent
	case "error":
		loglevel = LogLevelError
	case "warning":
		loglevel = LogLevelWarning
	// everything else (including invalid log levels) should default to verbose
	default:
		loglevel = LogLevelVerbose
	}

	logger = newLogger(buildPath, loglevel)

	// start up our logging loop (so we can print out messages as necessary)
	go logger.logLoop()
}

// NOTE TO READER: all log functions will only display if the appropriate log
// level is set.  Most log functions will simply fail silently if below their
// appropriate log level.

// LogCompileError logs and a compilation error (user-induced, bad code)
func LogCompileError(lctx *LogContext, message string, kind int, pos *TextPosition) {
	logger.logMsgChan <- &CompileMessage{
		Message:  message,
		Kind:     kind,
		Position: pos,
		Context:  lctx,
		IsError:  true,
	}
}

// LogCompileWarning logs a compilation warning (user-induced, problematic code)
func LogCompileWarning(lctx *LogContext, message string, kind int, pos *TextPosition) {
	logger.logMsgChan <- &CompileMessage{
		Message:  message,
		Kind:     kind,
		Position: pos,
		Context:  lctx,
		IsError:  false,
	}
}

// LogInternalError logs an error related to some non-compilation related
// failure (usually configuration)
func LogInternalError(kind, message string) {
	logger.logMsgChan <- &InternalError{Kind: kind, Message: message}
}

// LogFatal logs a fatal error message (something unexpected happened with the
// compiler -- developer error, requires bug fix).  TERMINATES PROGRAM!
func LogFatal(message string) {
	logger.logMsgChan <- &FatalError{Message: message}
	os.Exit(-1)
}

// LogInfo logs the inital info about the compiler and compilation process.
// This should be called at the start of compilation (for verbose logging).
func LogInfo(targetOS, targetArch string, debug bool) {
	if logger.LogLevel == LogLevelVerbose {
		displayCompilationInfo(targetOS, targetArch, debug)
	}
}

// LogStateChange logs a state change (for verbose logging)
func LogStateChange(newstate string) {
	if logger.LogLevel == LogLevelVerbose {
		displayStateChange(logger.prevUpdate, newstate)

		// always update timer
		logger.prevUpdate = time.Now()
	}
}

// LogFinished logs the final status of compilation and displays any warnings
// encountered.  This should be called at the end of compilation (regardless of
// success or failure).
func LogFinished() {
	if logger.LogLevel > LogLevelError {
		for _, warning := range logger.warnings {
			warning.display()
		}
	}

	if logger.LogLevel > LogLevelSilent {
		displayFinalMessage(logger.ErrorCount, len(logger.warnings))
	}
}

// ShouldProceed checks if there are any errors in the logger that should block
// compilation from proceeding -- should be called at the end of each stage of
// compilation and after any call to `compiler.initPackage`
func ShouldProceed() bool {
	return logger.ErrorCount == 0
}
