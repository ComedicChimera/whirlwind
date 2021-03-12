package mods

import "whirlwind/syntax"

// Note: Module files are essentially just YAML config files that are used to
// store all the data that describes the module.  The possible fields of the
// module file are: `name` (module name - required), `custom_paths` (maps import
// paths to other paths; used for overriding imports in a module -- optional).

// moduleFileName is the name of the module file
const moduleFileName = "whirl-mod.yml"

// Module represents a single Whirlwind module.  It stores all the data
// extracted from the module file so that it can be used during compilation
type Module struct {
	Name, Path string

	// PathOverrides stores all the custom path overrides in the module (to
	// replace an import path within the module with a different path)
	PathOverrides map[string]string
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
