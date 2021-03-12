package mods

import (
	"fmt"
	"os"
	"path/filepath"
	"whirlwind/logging"
)

// Module represents a single Whirlwind module.  It only stores the minimum
// amount of data required to identify the module as that is its sole purpose in
// compilation.  `Path` should be an absolute path
type Module struct {
	Name, Path string
}

// LoadModule attempts to load a module in the given package directory.  If no
// module is found or some other error occurs, the return flag is false.  `path`
// must be an absolute path
func LoadModule(path string) (*Module, bool) {
	modulePath := filepath.Join(path, "whirl.mod")

	finfo, err := os.Stat(modulePath)

	if err != nil {
		// `whirl.mod` does not exist => no module; no error
		if os.IsNotExist(err) {
			return nil, false
		}

		// something else went wrong, report it as fatal
		logging.LogFatal("Fatal error loading module: " + err.Error())
		return nil, false
	}

	// some directory named `whirl.mod` instead of a file
	if finfo.IsDir() {
		return nil, false
	}

	nameBytes, err := os.ReadFile(modulePath)

	if err != nil {
		// weird error loading module that didn't occur during os.Stat should be
		// reported...
		logging.LogFatal("Fatal error loading module: " + err.Error())
		return nil, false
	}

	name := string(nameBytes)

	// check for an invalid module name and log it but do not exit fatally as we
	// can simply continue compilation as if there was no module there
	if !IsValidPackageName(name) {
		logging.LogStdError(fmt.Errorf("Invalid module name `%s`", name))
		return nil, false

	}

	return &Module{
		Name: name,
		Path: path,
	}, true
}
