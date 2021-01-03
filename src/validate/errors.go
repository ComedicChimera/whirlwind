package validate

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/logging"
)

// LogUndefined logs an undefined error for the given symbol.
func (w *Walker) LogUndefined(name string, pos *logging.TextPosition) {
	logging.LogError(w.Context, fmt.Sprintf("Symbol `%s` undefined", name), logging.LMKName, pos)
}

// LogNotVisibleInPackage logs an import error in which is a symbol is not able
// to be imported from a foreign package.
func (w *Walker) LogNotVisibleInPackage(symname, pkgname string, pos *logging.TextPosition) {
	logging.LogError(
		w.Context,
		fmt.Sprintf("Symbol `%s` is not externally visible in package `%s`", symname, pkgname),
		logging.LMKName,
		pos,
	)
}

// logRepeatDef logs an error indicate that a symbol has already been defined
func (w *Walker) logRepeatDef(name string, pos *logging.TextPosition, topDefError bool) {
	w.fatalDefError = topDefError

	logging.LogError(
		w.Context,
		fmt.Sprintf("Symbol `%s` already defined", name),
		logging.LMKName,
		pos,
	)
}

// LogInvalidIntrinsic marks that the given named type cannot be intrinsic.
// Sets `fatalDefError`.
func (w *Walker) logInvalidIntrinsic(name, kind string, pos *logging.TextPosition) {
	w.fatalDefError = true

	logging.LogError(
		w.Context,
		fmt.Sprintf("No intrinsic %s by name `%s`", kind, name),
		logging.LMKUsage,
		pos,
	)
}
