package logging

import (
	"os"
	"time"
)

// logger is a global reference to a shared Logger (created/initialized with the
// compiler, but separated for general usage)
var logger Logger

// Initialize initializes the global logger with the provided log level
func Initialize(loglevelname string) {
	logger = Logger{}

	switch loglevelname {
	case "silent":
		logger.LogLevel = LogLevelSilent
	case "error":
		logger.LogLevel = LogLevelError
	case "warning":
		logger.LogLevel = LogLevelWarning
	// everything else (including invalid log levels) should default to verbose
	default:
		logger.LogLevel = LogLevelVerbose
	}
}

// NOTE TO READER: all log functions will only display if the appropriate log
// level is set.  Most log functions will simply fail silently if below their
// appropriate log level.

// LogError logs and displays a compilation error (user-induced, bad code)
func LogError(lctx *LogContext, message string, kind int, pos *TextPosition) {
	logger.ErrorCount++

	if logger.LogLevel > LogLevelSilent {
		displayLogMessage(&LogMessage{Context: lctx, Message: message, Kind: kind, Position: pos}, true)
	}
}

// LogStdError logs a standard Go `error` appropriately.  If it is a LogMessage,
// this function has the same behavior as `LogError`.  If it is not, it will be
// printed without any boxing. NOTE: `err` should NOT be `nil` -- this function
// will panic if it is.
func LogStdError(err error) {
	if logger.LogLevel > LogLevelSilent {
		if lm, ok := err.(*LogMessage); ok {
			logger.ErrorCount++
			displayLogMessage(lm, true)
		} else {
			displayStdError(err)
		}
	}
}

// LogWarning logs a compilation warning (user-induced, less-than-ideal code)
func LogWarning(lctx *LogContext, message string, kind int, pos *TextPosition) {
	logger.Warnings = append(logger.Warnings, &LogMessage{
		Context:  lctx,
		Message:  message,
		Kind:     kind,
		Position: pos,
	})
}

// LogFatal logs a fatal error message (something unexpected happened with the
// compiler -- developer error, requires bug fix).  TERMINATES PROGRAM!
func LogFatal(message string) {
	displayFatalMessage(message)
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
		for _, warning := range logger.Warnings {
			displayLogMessage(warning, false)
		}
	}

	if logger.LogLevel > LogLevelSilent {
		displayFinalMessage(logger.ErrorCount, len(logger.Warnings))
	}
}
