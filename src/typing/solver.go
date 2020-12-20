package typing

// Solver is the abstraction responsible for inferring the types of an given
// block or context.  It is also referred to as the inferencer.
type Solver struct {
	// Equations is a list of the active/unsolved type equations
	Equations []*TypeEquation

	// GlobalBindings is a reference to all of the bindings declared at a global
	// level in the current package
	GlobalBindings *BindingRegistry

	// LocalBindings is a reference to all of the bindings that are imported
	// from other packages are only visible in the current file
	LocalBindings *BindingRegistry
}
