package syntax

import (
	"bufio"
	"encoding/gob"
	"os"
	"strings"
)

// loadParsingTable takes a path to a parsing table file and loads the data into
// a new parsing table and returns the result (if there was an error, it is
// returned)
func loadParsingTable(path string) (ParsingTable, error) {
	file, err := os.Open(path)

	if err != nil {
		return nil, err
	}

	pt := make(ParsingTable)
	r := bufio.NewReader(file)

	err = gob.NewDecoder(r).Decode(pt)

	return pt, err
}

// saveParsingTable takes a path and a preexisting parsing table and dumps that
// parsing into a file at that path. If the file does not exist, it is created.
// In it does exist, it overwritten and truncated.
func saveParsingTable(path string, pt ParsingTable) error {
	file, err := os.Create(path)

	if err != nil {
		return err
	}

	defer file.Close()
	w := bufio.NewWriter(file)

	err = gob.NewEncoder(w).Encode(pt)

	return err
}

// getParsingTablePath takes a path to a grammar and determines the path to the
// corresponding parsing table.  Note that this function does not determine
// whether or not the table exists.
func getParsingTablePath(gPath string) string {
	return strings.Replace(gPath, ".ebnf", ".ptable", 1)
}
