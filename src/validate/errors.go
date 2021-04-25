package validate

import (
	"fmt"

	"whirlwind/logging"
	"whirlwind/typing"
)

// LogUndefined logs an undefined error for the given symbol.
func (w *Walker) LogUndefined(name string, pos *logging.TextPosition) {
	logging.LogCompileError(w.Context, fmt.Sprintf("Symbol `%s` undefined", name), logging.LMKName, pos)
}

// LogNotVisibleInPackage logs an import error in which is a symbol is not able
// to be imported from a foreign package.
func (w *Walker) LogNotVisibleInPackage(symname, pkgname string, pos *logging.TextPosition) {
	logging.LogCompileError(
		w.Context,
		fmt.Sprintf("Symbol `%s` is not externally visible in package `%s`", symname, pkgname),
		logging.LMKName,
		pos,
	)
}

// logRepeatDef logs an error indicate that a symbol has already been defined
func (w *Walker) logRepeatDef(name string, pos *logging.TextPosition) {
	logging.LogCompileError(
		w.Context,
		fmt.Sprintf("Symbol `%s` already defined", name),
		logging.LMKName,
		pos,
	)
}

// logInvalidIntrinsic marks that the given named type cannot be intrinsic.
// Sets `fatalDefError`.
func (w *Walker) logInvalidIntrinsic(name, kind string, pos *logging.TextPosition) {
	w.logError(
		fmt.Sprintf("No intrinsic %s by name `%s`", kind, name),
		logging.LMKUsage,
		pos,
	)
}

// logCoercionError logs an error coercing from one type to another
func (w *Walker) logCoercionError(src, dest typing.DataType, pos *logging.TextPosition) {
	w.logError(
		fmt.Sprintf("Unable to coerce from `%s` to `%s`", src.Repr(), dest.Repr()),
		logging.LMKTyping,
		pos,
	)
}

// logUnsolvableGenericTypeParam logs that a the value of a type parameter could
// not be determined by the solver
func (w *Walker) logUnsolvableGenericTypeParam(gt *typing.GenericType, name string, pos *logging.TextPosition) {
	w.logError(
		fmt.Sprintf("Unable to infer type for generic parameter `%s` of `%s`", name, gt.Repr()),
		logging.LMKTyping,
		pos,
	)
}

// logUndeterminedNull logs an error indicating that no type was able to be determined for `null`
func (w *Walker) logUndeterminedNull(pos *logging.TextPosition) {
	w.logError(
		"Unable to infer type of `null`",
		logging.LMKTyping,
		pos,
	)
}

// logError logs an error of any kind within the walker's file
func (w *Walker) logError(message string, kind int, pos *logging.TextPosition) {
	logging.LogCompileError(
		w.Context,
		message,
		kind,
		pos,
	)
}
