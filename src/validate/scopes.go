package validate

import (
	"whirlwind/common"
	"whirlwind/typing"
)

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

	// FuncCtx stores the enclosing function for this scope.  It is used to access
	// parameters and perform return checking.  The function reference will be passed
	// down to all subscopes (unless a sub-function is created)
	FuncCtx *typing.FuncType
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
		FuncCtx:    cs.FuncCtx,
	})
}

// pushFuncScope pushes the containing scope of a function.  This should be
// called before any local scopes are pushed
func (w *Walker) pushFuncScope(funcCtx *typing.FuncType) {
	w.scopeStack = append(w.scopeStack, &Scope{
		Symbols: make(map[string]*common.Symbol),
		FuncCtx: funcCtx,
	})
}

// popScope pop a scope once it has been exited.  This function requires at
// least one scope in the scope stack
func (w *Walker) popScope() {
	w.scopeStack = w.scopeStack[:len(w.scopeStack)-1]
}

// defineLocal defines a local symbol or variable.  It assumes an enclosing
// scope has already been created (will panic otherwise)
func (w *Walker) defineLocal(name string, dt typing.DataType, constant bool) bool {
	// check for symbol collision (in the same scope).  We don't need to check
	// the function context since local variables will always shadow function
	// arguments (they exist in a sort of "psuedo-scope" above regular local
	// variables)
	if _, ok := w.currScope().Symbols[name]; ok {
		return false
	}

	// add our new local symbol to the scope -- local symbols will always be
	// named values (of some form) and will always be local (so we can assume
	// those values when creating our symbol)
	w.scopeStack[len(w.scopeStack)-1].Symbols[name] = &common.Symbol{
		Name:       name,
		Constant:   constant,
		Type:       dt,
		DeclStatus: common.DSLocal,
		DefKind:    common.DefKindNamedValue,
	}
	return true
}
