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
