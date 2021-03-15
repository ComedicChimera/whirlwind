package mods

import (
	"errors"
	"fmt"
	"os"
	"path/filepath"
	"whirlwind/logging"

	"gopkg.in/yaml.v2"
)

// LoadModule attempts to load a module in the given package directory.  If no
// module is found or some other error occurs, the return flag is false.  `path`
// must be an absolute path
func LoadModule(path string) (*Module, bool) {
	modulePath := filepath.Join(path, moduleFileName)

	finfo, err := os.Stat(modulePath)

	if err != nil {
		// module file does not exist => no module; no error
		if os.IsNotExist(err) {
			return nil, false
		}

		// something else went wrong, report it as fatal
		logging.LogFatal("Fatal error loading module: " + err.Error())
		return nil, false
	}

	// some directory named `whirl-mod.yml` instead of a file
	if finfo.IsDir() {
		return nil, false
	}

	fbytes, err := os.ReadFile(modulePath)

	if err != nil {
		// weird error loading module that didn't occur during os.Stat should be
		// reported...
		logging.LogFatal("Fatal error loading module: " + err.Error())
		return nil, false
	}

	// unmarshall the yaml into a map
	moduleData := make(map[string]interface{})
	err = yaml.Unmarshal(fbytes, &moduleData)

	// invalid yaml -- log error and but do not exit fatally as we can simply
	// continue compilation as if there was no module
	if err != nil {
		logging.LogStdError(fmt.Errorf("YAML Error Decoding module: %s", err))
		return nil, false
	}

	mod, err := parseModuleYAML(moduleData, path)

	// any module parsing errors are not fatal: they should simply be logged and
	// then the compiler should proceed as if there is no module there
	if err != nil {
		logging.LogStdError(err)
		return nil, false
	}

	return mod, true
}

// parseModuleYAML walks the unmarshaled yaml data of the module
func parseModuleYAML(modData map[string]interface{}, path string) (*Module, error) {
	if nameField, ok := modData["name"]; ok {
		if name, ok := nameField.(string); ok {
			if !IsValidPackageName(name) {
				return nil, fmt.Errorf("Invalid module name: `%s`", name)
			}

			mod := &Module{
				Name:          name,
				Path:          path,
				PathOverrides: make(map[string]string),
			}

			customPathsField, ok := modData["custom_paths"]
			if ok {
				if customPaths, ok := customPathsField.(map[string]string); ok {
					for relPath, cpath := range customPaths {
						absCPath, err := filepath.Abs(cpath)

						if err != nil {
							return nil, fmt.Errorf("Bad custom path: %s", err.Error())
						}

						mod.PathOverrides[relPath] = absCPath
					}
				} else {
					return nil, fmt.Errorf("Module field `custom_paths` must be a mapping of path strings")
				}
			}

			return mod, nil
		}

		return nil, errors.New("Module field `name` must be a string")
	}

	return nil, errors.New("Module missing required field `name`")
}
