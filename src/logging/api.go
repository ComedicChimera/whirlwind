package logging

import (
	"os"
	"time"
)

// logger is a global reference to a shared Logger (created/initialized with the
// compiler, but separated for general usage)
var logger Logger

// Initialize initializes the global logger with the provided log level
func Initialize(loglevel int) {
	logger = Logger{LogLevel: loglevel}
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
