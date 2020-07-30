package analysis

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// Lookup looks up a symbol from the current walker context. Returns `nil` if no
// symbol is found.  Searches all scopes.  NOTE: should be used exclusively for
// read lookups not mutation/write lookups.
func (w *Walker) Lookup(name string) *common.Symbol {
	for i := len(w.Scopes) - 1; i > -1; i++ {
		if sym, ok := w.Scopes[i].Symbols[name]; ok {
			return sym
		}
	}

	if gsym, ok := w.Builder.Pkg.GlobalTable[name]; ok {
		return gsym
	}

	if rsym, ok := w.Builder.ResolvingSymbols[name]; ok {
		return rsym.SymRef
	}

	return nil
}

// Define creates a new symbol in the given scope and returns a bool indicating
// whether or not such a definition is possible.
func (w *Walker) Define(sym *common.Symbol) bool {
	if len(w.Scopes) == 0 {
		if _, ok := w.Builder.Pkg.GlobalTable[sym.Name]; ok {
			return false
		}

		// make sure any resolving symbols and/or remote symbols are handled
		if rs, ok := w.Builder.ResolvingSymbols[sym.Name]; ok {
			*rs.SymRef = *sym
			delete(w.Builder.ResolvingSymbols, sym.Name)
		}

		if remsym, ok := w.Builder.Pkg.RemoteSymbols[sym.Name]; ok {
			*remsym = *sym
			delete(w.Builder.Pkg.RemoteSymbols, sym.Name)
		}
	}

	cs := w.CurrScope()

	if _, ok := cs.Symbols[sym.Name]; ok {
		return false
	}

	cs.Symbols[sym.Name] = sym
	return true
}

// PushScope adds a new working scope to the scope stack
func (w *Walker) PushScope() {
	w.Scopes = append(w.Scopes, &Scope{
		Symbols:     make(map[string]*common.Symbol),
		NonNullRefs: make(map[string]struct{}),
		Kind:        SKUnknown,
		CtxFunc:     w.CurrScope().CtxFunc, // copy the CtxFunc reference
	})
}

// PushFuncScope adds a new function scope to the stack
func (w *Walker) PushFuncScope(ctxfn *types.FuncType, sk int) {
	w.Scopes = append(w.Scopes, &Scope{
		Symbols:     make(map[string]*common.Symbol),
		NonNullRefs: make(map[string]struct{}),
		Kind:        sk,
		CtxFunc:     ctxfn,
	})
}

// PopScope removes a working scope from the scope stack.
// NOTE: Does NOT handle function constancy.
func (w *Walker) PopScope() {
	w.Scopes = w.Scopes[:len(w.Scopes)-1]
}

// CurrScope returns the current working scope.  (NOTE: will not return the
// GlobalTable as a scope: if the scope stack is empty => PANIC)
func (w *Walker) CurrScope() *Scope {
	return w.Scopes[len(w.Scopes)-1]
}

// ThrowMultiDefError will log an error indicating that a symbol of a given name
// is declared multiple times in the current scope.
func ThrowMultiDefError(name string, pos *util.TextPosition) {
	util.ThrowError(
		fmt.Sprintf("Symbol `%s` declared multiple times", name),
		"Name",
		pos,
	)
}
