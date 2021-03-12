package mods

import (
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"whirlwind/logging"

	"gopkg.in/yaml.v2"
)

// CreateModule creates a new directory for a module and initializes it with
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

// InitModule initializes a module with a given name in the given directory
func InitModule(name, path string) error {
	// first we need to check if the module will be valid
	if !IsValidPackageName(name) {
		return fmt.Errorf("`%s` is not a valid module name", name)
	}

	// all modules contain their "definitions" in the module file
	fpath := filepath.Join(path, moduleFileName)

	// check if the path to the module file already exists => we don't want to
	// override a preexisting file with this command
	finfo, err := os.Stat(fpath)
	if err == nil {
		if finfo.IsDir() {
			return fmt.Errorf("Unable to overwrite directory named `%s` to initialize module", moduleFileName)
		} else {
			return errors.New("Module already exists")
		}
	} else if os.IsNotExist(err) {
		// convert our module to yaml
		modData, err := yaml.Marshal(map[string]string{"name": name})

		if err != nil {
			logging.LogFatal("Failed to marshall module YAML")
		}

		// if it does not exist, we can go ahead and create it
		return os.WriteFile(fpath, modData, 0644)
	}

	// some weird os error (idk)
	return err
}
