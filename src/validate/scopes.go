package validate

import "whirlwind/common"

// Scope represents a single lexical scope below the global scope (eg. the scope
// of a function or an if statement)
type Scope struct {
	// Symbols stores the symbols declared within this scope
	Symbols map[string]*common.Symbol

	// LoopScope indicates that this scope is the scope of a loop or a subscope
	// of a loop.  This is used for validating contextual keywords (eg. `break`)
	LoopScope bool

	// MatchScope indicates that this scope is the scope of a match statement or
	// a subscope of a match statement.  This is used for validating contextual
	// ketwords (eg. `fallthrough`)
	MatchScope bool
}

// currScope gets the current enclosing scope.  This assumes such a scope exists
// -- if it doesn't, this function will cause a panic
func (w *Walker) currScope() *Scope {
	return w.scopeStack[len(w.scopeStack)-1]
}

// pushScope pushes a local scope as a subscope of a function.  This function
// will cause a panic if used outside a preexisting function scope
func (w *Walker) pushScope() {
	cs := w.currScope()

	w.scopeStack = append(w.scopeStack, &Scope{
		Symbols: make(map[string]*common.Symbol),

		// propagate scope variables down
		LoopScope:  cs.LoopScope,
		MatchScope: cs.MatchScope,
	})
}

// pushFuncScope pushes the containing scope of a function.  This should be
// called before any local scopes are pushed
func (w *Walker) pushFuncScope() {
	w.scopeStack = append(w.scopeStack, &Scope{
		Symbols: make(map[string]*common.Symbol),
	})
}

// popScope pop a scope once it has been exited.  This function requires at
// least one scope in the scope stack
func (w *Walker) popScope() {
	w.scopeStack = w.scopeStack[:len(w.scopeStack)-1]
}
