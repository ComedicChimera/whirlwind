package cmd

import "fmt"

const helpMessage = `
whirl is a tool for managing Whirlwind source code

Usage:

	whirl <command> [arguments]

The commands are:

	build      compile packages and modules
	check      check packages and output errors
	clean      remove object files and cached data
	del        delete installed modules
	fetch      fetch and install a remote module
	make       compile intermediates (asm, object, etc.)
	mod        manage modules
	run        compile and run packages and modules
	test       test packages and modules
	update     update or rollback whirl
	version    print the version of whirl
`

// printHelpMessage prints the general purpose help message
func printHelpMessage() {
	fmt.Println(helpMessage)
}

const modHelpMessage = `
Usage:

	whirl mod <subcommand> [arguments]

The subcommands are:

	del       delete the current module (not source files)
	init      initialize a new module in the current directory
	new       create a new directory with a module of the same name initialized in it
	rename    renames the current module
`

// printModHelpMessage prints the help message for the `mod` command when it is
// missing or has an invalid a subcommand
func printModHelpMessage() {
	fmt.Println(modHelpMessage)
}
