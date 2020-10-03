package assemble

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/logging"
	"github.com/ComedicChimera/whirlwind/src/syntax"
)

// The Resolution Algorithm (Single-Package)
// -----------------------------------------
// 1. Pass through the package walking all top-level definitions.
//   i. If all the necessary items are defined for the definition
//   to be created, declare it and add it as a HIRNode.
//   ii. If not, add it to the definition queue (as a definition)
// 2. Pass over the queue until no more definitions can be declared.
//   i. If all the necessary items are defined for the definition
//   at the front of the queue, declare it and add it as a HIRNode.
//   Then remove that definition from the queue.
//   ii. If some symbols are still unresolved, rotate it to the back
//   of the queue and proceed to the next item.
// 3. Pass over all remaining definitions and determine their states.
//   i. State: External -- Definition requires external definition
//   (via explict or namespace import) => accept definition;
//   resolution succeeds, proceed to cross resolution.
//   ii. State: Undefined - Definition requires undefined symbols =>
//   reject definition; resolution fails.
//   iii. State: Cyclic: Definitions are mutually dependent =>
//   reject definition; resolution fails.
//   iv. If no definitions remain, resolution succeeds.

// Resolver is a type responsible for resolving all of the symbol data in a single
// package.  It cannot cross-resolve on its own -- it is mostly just a data store.
type Resolver struct {
	// Table is the resolution table used to manage resolving symbols
	Table *ResolutionTable

	// DefQueue is used to store the resolving definitions
	DefQueue *DefinitionQueue
}

// NewResolverForPackage creates a new resolver for the given package
func NewResolverForPackage(pkg *common.WhirlPackage) *Resolver {
	return &Resolver{
		Table:    &ResolutionTable{CurrPkg: pkg},
		DefQueue: &DefinitionQueue{},
	}
}

// GetPackage returns the package reference stored by the Resolver
func (r *Resolver) GetPackage() *common.WhirlPackage {
	return r.Table.CurrPkg
}

// ResolveLocals walks through the package and attempts to resolve all local
// symbols that are known and unknown using the single-package symbol resolution
// algorithm.  When this algorithm is finished, the Resolver's definition queue
// will be populated with the all of the symbols that rely on external
// definitions.  Additionally, all of the known definitions will be walked,
// converted into HIR nodes and added to the appropriate file's HIR root.  This
// function will mark any symbols that are definitively unresolveable.
func (r *Resolver) ResolveLocals() bool {
	r.initialPass()
	r.resolutionPass()

	// stage 3 of resolution algorithm below
	if r.DefQueue.Len() > 0 {
		// used to keep track of all of the symbols that are not accounted for
		// as unknowns so we can determine whether the error is a cyclic
		// definition error or a symbol undefined error.  We store the
		// error message to throw for each symbol as an efficient way to
		// avoid unnecessary branching and positional data.
		unaccountedUnknowns := make(map[string]*logging.LogMessage)

		for i := 0; i < r.DefQueue.Len(); i++ {
			top := r.DefQueue.Peek()

			// check if any of this definition's names are in the unaccounted
			// unknowns => cyclic definition -- two symbols depend on each other
			// in an unresolveable way :(.
			for _, name := range top.DefNames {
				if uerr, ok := unaccountedUnknowns[name]; ok {
					// update the error to be a cyclic definition error
					uerr.Message = fmt.Sprintf("Symbol `%s` defined cyclically", name)
				}
			}

			for name, pos := range top.RequiredSymbols {
				// check if the symbol is externally defined -- "null" reference.
				// This happens after the cyclic check as we know that it
				if sym, ok := top.SrcFile.LocalTable[name]; ok && sym.Name == "" {
					continue
				}

				// if it is not an externally defined symbol, we need to mark it
				// as unaccounted for if it hasn't been already marked as such
				if _, ok := unaccountedUnknowns[name]; !ok {
					lctx := &logging.LogContext{
						PackageID: r.GetPackage().PackageID,
						FilePath:  top.SrcFilePath,
					}
					unaccountedUnknowns[name] = lctx.CreateMessage(
						fmt.Sprintf("Symbol `%s` undefined", name),
						logging.LMKName,
						pos,
					)
				}
			}

			r.DefQueue.Rotate()
		}

		// throw all undefined error as necessary and error out
		if len(unaccountedUnknowns) > 0 {
			for _, lm := range unaccountedUnknowns {
				logging.LogStdError(lm)
			}

			return false
		}
	}

	return true
}

// initialPass performs step 1 of the resolution algorithm
func (r *Resolver) initialPass() {
	for fpath, wfile := range r.GetPackage().Files {
		wfile.Root = &common.HIRRoot{}

		// pass over all of the export blocks as necessary before passing over
		// the file as a whole.
		for _, item := range wfile.AST.Content {
			itembranch := item.(*syntax.ASTBranch)

			if itembranch.Name == "export_block" {
				r.initialPassOverBlock(fpath, wfile, itembranch.BranchAt(3))
			} else {
				// we know this is the "top_level" node and we do not need to
				// expect any other export blocks.
				r.initialPassOverBlock(fpath, wfile, itembranch)
			}
		}

		// free the top AST -- it is no longer needed :)
		wfile.AST = nil
	}
}

// initialPassOverBlock takes an AST over which to perform the initial
// resolution pass (walks a top_level `node`)
func (r *Resolver) initialPassOverBlock(fpath string, wfile *common.WhirlFile, block *syntax.ASTBranch) {
	for _, topast := range block.Content {
		branch := topast.(*syntax.ASTBranch)
		syms, hirn, unknowns := r.walkDef(branch)
		if hirn == nil {
			// create our definition's defined names slice
			defNames := make([]string, len(syms))
			n := 0
			for sym := range syms {
				defNames[n] = sym.Name
				n++
			}

			r.DefQueue.Enqueue(&Definition{
				Branch:          branch,
				DefNames:        defNames,
				RequiredSymbols: unknowns,
				SrcFile:         wfile,
				SrcFilePath:     fpath,
			})
		} else {
			// all declaration errors handled by declare
			r.declare(fpath, wfile, syms, hirn)
		}
	}
}

// resolutionPass performs step 2 of the resolution algorithm
func (r *Resolver) resolutionPass() {
	// mark is a variable used to keep track of the first symbol rotated to the
	// back on each pass.  If the mark is encountered again and length of the
	// queue has not changed (no new definitions declared), then this stage
	// exits.
	var mark *Definition
	prevLen := r.DefQueue.Len() // store the previous length for later comparison

	for r.DefQueue.Len() > 0 {
		top := r.DefQueue.Peek()

		if r.resolveDef(top) {
			// if we resolve our mark, we have to set it to nil so as not to get
			// caught in an infinite loop -- update our resolution state
			// appropriately.
			if top == mark {
				mark = nil
			}

			r.DefQueue.Dequeue()
		} else {
			// if our mark is `nil`, we need to initialize it
			if mark == nil {
				mark = top
			} else if mark == top {
				// otherwise, if our we have hit our mark and the length hasn't
				// changed, we know we can't resolve anything else so we exit
				if prevLen == r.DefQueue.Len() {
					break
				}

				// if the length has changed, then we resolved something and can
				// keep passing through
				prevLen = r.DefQueue.Len()
			}

			r.DefQueue.Rotate()
		}
	}
}

// resolveDef attempts to resolve and declare a definition
func (r *Resolver) resolveDef(def *Definition) bool {
	// first, update the required symbols of this definition to account for any
	// newly defined symbols -- this will inform us on the status of this
	// definition and allow for more efficient checking and error handling later.
	for req := range def.RequiredSymbols {
		if _, ok := r.Table.Lookup(req); ok {
			// The mutation here is unproblematic as this element has already
			// been handled (so we can delete freely)
			delete(def.RequiredSymbols, req)
		}
	}

	// if there are still unknown required symbols, then this definition is is
	// not ready for resolution, and we indicate as such
	if len(def.RequiredSymbols) > 0 {
		return false
	}

	// this should always succeed if all required symbols exist
	syms, hirn, _ := r.walkDef(def.Branch)

	// even if declaration fails, we still want to "succeed here" so that this
	// definition doesn't linger in the queue.  It has been "resolved", it is
	// simply erroneous.  All of its dependencies will not resolve and so its
	// consequences will be handled there.
	r.declare(def.SrcFilePath, def.SrcFile, syms, hirn)
	return true
}

// declare finalizes and declares a finished definition. `fpath` is needed to
// throw errors on any symbol that declared multiple times in the global scope.
func (r *Resolver) declare(fpath string, wfile *common.WhirlFile, syms map[*common.Symbol]*logging.TextPosition, hirn common.HIRNode) {
	// create a reusable log context for this declared
	lctx := &logging.LogContext{
		PackageID: r.GetPackage().PackageID,
		FilePath:  fpath,
	}

	allok := true
	for sym, pos := range syms {
		if !r.Table.Define(sym) {
			logging.LogError(lctx, fmt.Sprintf("Symbol `%s` declared multiple times", sym.Name), logging.LMKName, pos)
			allok = false
		}
	}

	if allok {
		// only want to finalize the definition if the symbols are all declared "ok"
		wfile.Root.Elements = append(wfile.Root.Elements, hirn)
	}
}

// logUnresolved considers the resolution stage finished for this resolver and
// causes it to log the appropriate errors for all unresolved imports or symbols
// referenced via. namespace imports.
func (r *Resolver) logUnresolved() {

}
