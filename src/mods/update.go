package mods

import (
	"errors"
	"fmt"
	"os"
)

func Del() error {
	finfo, err := os.Stat(moduleFileName)

	if err == nil {
		if finfo.IsDir() {
			return fmt.Errorf("`%s` must be a file", moduleFileName)
		}

		return os.Remove(moduleFileName)
	} else if os.IsNotExist(err) {
		return errors.New("No module exists in the current directory")
	}

	return err
}
