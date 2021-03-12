package mods

import (
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"whirlwind/syntax"
)

// NOTE: The `whirl.mod` file is a literally just a text file with a name listed
// in it for the current module

// `CreateModule` creates a new directory for a module and initializes it with
// the given name
func CreateModule(name string) error {
	finfo, err := os.Stat(name)

	// we want the directory to not exist so we can create a new module
	if os.IsNotExist(err) {
		err = os.Mkdir(name, os.ModeDir)
		if err != nil {
			return err
		}

		return InitModule(name, name)
	} else if err != nil {
		// some other os error
		return err
	}

	// `init` should be used to turn a preexisting directory into a module
	// TODO: allow for `new` to work "as intended" here anyway?
	if finfo.IsDir() {
		return fmt.Errorf("Directory by name `%s` already exists.  Use `mod init` to initialize it", name)
	}

	// files can never be modules
	return fmt.Errorf("File by name `%s` already exists and cannot be a module", name)
}

// `InitModule` initializes a module with a given name in the given directory
func InitModule(name, path string) error {
	// first we need to check if the module will be valid
	if !IsValidPackageName(name) {
		return fmt.Errorf("`%s` is not a valid module name", name)
	}

	// all modules contain their "definitions" in `whirl.mod`
	fpath := filepath.Join(path, "whirl.mod")

	// check if the path to `whirl.mod` already exists => we don't want to
	// override a preexisting file with this command
	finfo, err := os.Stat(fpath)
	if err == nil {
		if finfo.IsDir() {
			return errors.New("Unable to overwrite directory named `whirl.mod` to initialize module")
		} else {
			return errors.New("Module already exists")
		}
	} else if os.IsNotExist(err) {
		// if it does not exist, we can go ahead and create it
		return os.WriteFile(fpath, []byte(name), 0644)
	}

	// some weird os error (idk)
	return err
}

// IsValidPackageName checks if a name is valid for a package (or module).
// Specifically, this function tests if the name would be a valid (usable)
// identifier within Whirlwind (as a package must be referenceable by name in
// the language)
func IsValidPackageName(name string) bool {
	if syntax.IsLetter(rune(name[0])) || name[0] == '_' {
		for i := 1; i < len(name); i++ {
			if !syntax.IsLetter(rune(name[i])) && !syntax.IsDigit(rune(name[i])) && name[i] != '_' {
				return false
			}
		}

		return true
	}

	return false
}
