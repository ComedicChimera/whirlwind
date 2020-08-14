package analysis

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// Scope represents an enclosing local scope
type Scope struct {
	Symbols map[string]*common.Symbol

	// Context represents the context imposed by the scope
	Context *ScopeContext

	// ReturnsValue is used to indicate that a given scope/code-path returns a
	// value (deterministically - return always occurs; can be via `yield`)
	ReturnsValue bool
}

// ScopeContext is a construct used to store the contextual variables that make
// up the context created by a given scope.
type ScopeContext struct {
	// NonNullRefs stores all of the references who due to some enclosing operation
	// have been marked as non-null in contrast to their data type (by name)
	NonNullRefs map[string]struct{}

	// Func is the enclosing function of a scope.  May be `nil` under certain
	// circumstances (eg. inside an interface definition but outside of a method)
	Func *types.FuncType

	// EnclosingLoop indicates that the given scope represents a loop and
	// therefore `break` and `continue` should be valid in all subscopes.
	EnclosingLoop bool

	// EnclosingMatch indicates that the given scope represents a match
	// statement and therefore `fallthrough` should be valid in all subscopes.
	EnclosingMatch bool
}

// NewContext creates a new scope context based on an enclosing context
func (sc *ScopeContext) NewContext() *ScopeContext {
	nnr := make(map[string]struct{})
	for name := range sc.NonNullRefs {
		nnr[name] = struct{}{}
	}

	return &ScopeContext{
		Func:           sc.Func,
		NonNullRefs:    nnr,
		EnclosingLoop:  sc.EnclosingLoop,
		EnclosingMatch: sc.EnclosingMatch,
	}
}

// LoopContext creates a new scope context for a loop
func (sc *ScopeContext) LoopContext() *ScopeContext {
	newCtx := sc.NewContext()
	newCtx.EnclosingLoop = true
	return newCtx
}

// MatchContext creates a new scope context for an match statement
func (sc *ScopeContext) MatchContext() *ScopeContext {
	newCtx := sc.NewContext()
	newCtx.EnclosingMatch = true
	return newCtx
}

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

// PositionedName is a construct used in performing package lookups
type PositionedName struct {
	Name string
	Pos  *util.TextPosition
}

// GetSymbolFromPackage looks up a symbol based on the given list of positioned
// names where the names are assumed to make up a static-get expression.
func (w *Walker) GetSymbolFromPackage(pnames []PositionedName) (*common.Symbol, error) {
	retImportError := func(pname, sname string, pos *util.TextPosition) error {
		return util.NewWhirlError(
			fmt.Sprintf("Unable to import symbol `%s` from package `%s`", pname, sname),
			"Import",
			pos,
		)
	}

	retUndefError := func(sname string, pos *util.TextPosition) error {
		return util.NewWhirlError(
			fmt.Sprintf("Symbol `%s` undefined", sname),
			"Name",
			pos,
		)
	}

	var pkg *common.WhirlPackage
	for i, pname := range pnames {
		if i == len(pnames) {
			if sym, ok := pkg.GlobalTable[pname.Name]; ok && sym.VisibleExternally() {
				return sym, nil
			}

			return nil, retImportError(pkg.Name, pname.Name, pname.Pos)
		} else if pkg == nil {
			if vpkg, ok := w.File.VisiblePackages[pname.Name]; ok {
				pkg = vpkg
			} else {
				return nil, retUndefError(pname.Name, pname.Pos)
			}
		} else if wimport, ok := pkg.ImportTable[pname.Name]; ok && wimport.Exported {
			pkg = wimport.PackageRef
		} else {
			return nil, retImportError(
				pkg.Name, pname.Name, pname.Pos,
			)
		}
	}

	// unreachable
	return nil, nil
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
func (w *Walker) PushScope(ctx *ScopeContext) {
	w.Scopes = append(w.Scopes, &Scope{
		Symbols: make(map[string]*common.Symbol),
		Context: ctx,
	})
}

// PushFuncScope adds a new function scope to the stack
func (w *Walker) PushFuncScope(ctxfn *types.FuncType) {
	var newCtx *ScopeContext
	if len(w.Scopes) > 0 {
		newCtx = w.CurrScope().Context.NewContext()
		newCtx.Func = ctxfn
	} else {
		newCtx = &ScopeContext{Func: ctxfn}
	}

	w.Scopes = append(w.Scopes, &Scope{
		Symbols: make(map[string]*common.Symbol),
		Context: newCtx,
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
