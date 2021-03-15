package resolve

import (
	"whirlwind/common"
	"whirlwind/logging"
	"whirlwind/syntax"
	"whirlwind/typing"
)

// OpaqueDefinition is used to represent a definition that is declared as
// an opaque type.  Specifically, it includes the symbols the definition
// resolves (if declared) so that it can be used to prune these from
// the opaque graph as definitions fail to resolve
type OpaqueDefinition struct {
	Def      *Definition
	Resolves map[uint][]*Definition
}

// resolveCyclic runs stage 3 of the resolution algorithm: it attempts cyclic
// dependendency resolution on our top level definitions -- it is the "last
// resort" resolution pass.  It returns a boolean indicating whether all of the
// remaining type definitions will be resolved.  Regardless of this, all
// determinate definitions will be resolved by the time this function exits.
func (r *Resolver) resolveCyclic() bool {
	// opaqueDefs is a map of all definitions that have opaque types organized
	// by the package they are defined in
	opaqueDefs := r.createAllOpaques()

	// we don't actually delete each opaqueDef as we process it -- instead we
	// just set the table to `nil` at the end to dump it
	for pkgid, row := range opaqueDefs {
		for _, odef := range row {
			if !r.cyclicResolveDef(pkgid, odef.Def) {
				// pruning time!
				r.pruneUnresolveables(opaqueDefs, pkgid, odef.Def)
			}
		}
	}

	// delete table
	opaqueDefs = nil

	// resolve all remaining type definitions (step 3c)
	for pkgid, pa := range r.assemblers {
		for pa.DefQueue.Len() > 0 {
			// we don't individually care which resolve and which don't
			r.cyclicResolveDef(pkgid, pa.DefQueue.Peek())
			pa.DefQueue.Dequeue()
		}
	}

	// clear the opaque symbol table -- we no longer need them and mark all
	// walkers as having finished resolution
	for _, pa := range r.assemblers {
		for _, walker := range pa.walkers {
			walker.ResolutionDone()
		}
	}

	r.sharedOpaqueSymbolTable = nil

	return logging.ShouldProceed()
}

// createAllOpaques creates all of the opaques necessary for cyclic symbol
// resolution (ie. performs step 3a)
func (r *Resolver) createAllOpaques() map[uint]map[string]*OpaqueDefinition {
	// opaqueDefs stores all the opaque definitions (since the `Definition`
	// structs themselves are not stores in the sharedOpaqueSymbolTable)
	opaqueDefs := make(map[uint]map[string]*OpaqueDefinition)

	// initialize sub-maps since definitions might not be added in package order
	for pkgid := range r.assemblers {
		opaqueDefs[pkgid] = make(map[string]*OpaqueDefinition)
	}

	// first go through and simply create nil entries in opaqueDefs for all the
	// definitions that resolve something.  This is determined based on whether
	// or not that definition exists as a dependent to some other definition.
	// Also populate the resolve list as necessary
	for pkgid, pa := range r.assemblers {
		for i := 0; i < pa.DefQueue.Len(); i++ {
			def := pa.DefQueue.Peek()

			for _, dep := range def.Dependents {
				depPkgid := dep.SrcPackage.PackageID

				if odef, ok := opaqueDefs[depPkgid][dep.Name]; ok {
					if row, ok := odef.Resolves[pkgid]; ok {
						odef.Resolves[pkgid] = append(row, def)
					} else {
						odef.Resolves[pkgid] = []*Definition{def}
					}
				} else {
					opaqueDefs[depPkgid][dep.Name] = &OpaqueDefinition{
						Def: nil,
						Resolves: map[uint][]*Definition{
							pkgid: {def},
						},
					}
				}
			}

			pa.DefQueue.Rotate()
		}
	}

	// next, go through and populate all our definition entries in opaqueDefs
	// and create all our opaque symbols as necessary.  Also dequeue any
	// definitions that are made into opaque symbols so we know not to try to
	// resolve them again in step 3c
	for pkgid, pa := range r.assemblers {
		// mark stores the initial non-opaque definition we encounter so that
		// we know when we hit it again to stop processing this queue
		var mark *Definition

		for mark != pa.DefQueue.Peek() {
			def := pa.DefQueue.Peek()
			if odef, ok := opaqueDefs[pkgid][def.Name]; ok {
				odef.Def = def
				r.createOpaqueSymbol(pkgid, def)
				pa.DefQueue.Dequeue()
				continue
			} else if mark == nil {
				mark = pa.DefQueue.Peek()
			}

			pa.DefQueue.Rotate()
		}
	}

	// prune all definitions the depend on symbols not in the opaque definition table
	// since we know these symbols will never be resolveable
	for _, row := range opaqueDefs {
		for name, odef := range row {
			if odef.Def == nil {
				for pkgid, rrow := range odef.Resolves {
					for _, def := range rrow {
						r.logUnresolved(pkgid, def, def.Dependents[name])
						r.pruneUnresolveables(opaqueDefs, pkgid, def)
					}
				}

				delete(row, name)
			}
		}
	}

	return opaqueDefs
}

// createOpaqueSymbol takes a definition and creates and stores an appropriate
// opaque type for it.  It stores it in the shared opaque symbol table.
func (r *Resolver) createOpaqueSymbol(pkgid uint, def *Definition) {
	var generic, requiresRef bool

	typeRequiresRef := func(suffix *syntax.ASTBranch) bool {
		if suffix.Name == "newtype" {
			subSuffix := suffix.BranchAt(0)
			if subSuffix.Name == "struct_suffix" {
				// structs always require a reference
				return true
			}

			// algebraic types may require a reference if they only contain one
			// value -- same logic as for type sets
			return subSuffix.Len() > 1
		}

		// if the suffix is only length 2, then we have an alias not a type set
		// which means that a reference may be required (if this stores a struct
		// for example) and since we can't know until this type resolves, we
		// have to assume one is necessary
		return suffix.Len() == 2
	}

	switch def.Branch.LeafAt(0).Kind {
	case syntax.TYPE:
		generic = def.Branch.BranchAt(2).Name == "generic_tag"
		requiresRef = typeRequiresRef(def.Branch.Last().(*syntax.ASTBranch))
	case syntax.INTERF:
		if _, ok := def.Branch.Content[2].(*syntax.ASTBranch); ok {
			generic = ok
		}

		// interfaces never require a reference
	case syntax.CLOSED:
		generic = def.Branch.BranchAt(3).Name == "generic_tag"
		requiresRef = typeRequiresRef(def.Branch.Last().(*syntax.ASTBranch))
	}

	dependsOn := make(map[string][]uint)
	for name, dep := range def.Dependents {
		if pkgIDs, ok := dependsOn[name]; ok {
			dependsOn[name] = append(pkgIDs, dep.SrcPackage.PackageID)
		} else {
			dependsOn[name] = []uint{dep.SrcPackage.PackageID}
		}
	}

	var opaqueType typing.DataType
	if generic {
		opaqueType = &typing.OpaqueGenericType{}
	} else {
		opaqueType = &typing.OpaqueType{Name: def.Name}
	}

	r.sharedOpaqueSymbolTable[pkgid][def.Name] = &common.OpaqueSymbol{
		Name:         def.Name,
		Type:         opaqueType,
		SrcPackageID: pkgid,
		DependsOn:    dependsOn,
		RequiresRef:  requiresRef,
	}
}

// pruneUnresolveables recursively prunes all definitions that cannot be
// resolved because the definition passed in itself cannot be resolved. The
// `pkgid` is the package ID of the definition whose resolves we want to prune.
// It also prunes the original definition
func (r *Resolver) pruneUnresolveables(opaqueDefs map[uint]map[string]*OpaqueDefinition, pkgid uint, def *Definition) {
	if odef, ok := opaqueDefs[pkgid][def.Name]; ok {
		delete(opaqueDefs[pkgid], def.Name)

		// no opaque symbol has actually been declared so nothing to remove from
		// that table (only declared if matching def is found)

		for rpkgid, rrow := range odef.Resolves {
			for _, resolve := range rrow {
				r.logUnresolved(rpkgid, resolve, resolve.Dependents[def.Name])
				r.pruneUnresolveables(opaqueDefs, rpkgid, resolve)
			}
		}
	} else {
		// definition has already been pruned so we don't need to do anything
		return
	}
}

// cyclicResolveDef attempts to resolve and walk a cyclic definition. It will
// log appropriate errors if the definition fails to resolve
func (r *Resolver) cyclicResolveDef(pkgid uint, def *Definition) bool {
	// the only things that remain unresolved should be the the opaque
	// definitions (all others should've been removed from dependents during
	// resolution stage 2) -- so we can just lookup those; we do not want to
	// delete the dependents since otherwise if the opaque symbol fails to
	// resolve, this definition could still be unresolved itself and we want to
	// log it appropriately
	unresolved := make(map[string]struct{})
	for _, dep := range def.Dependents {
		if _, ok := r.sharedOpaqueSymbolTable.LookupOpaque(dep.SrcPackage.PackageID, dep.Name); !ok {
			unresolved[dep.Name] = struct{}{}
		}
	}

	// if there are no unresolved, then we can try to walk the definition
	if len(unresolved) == 0 {
		// if walking fails, then so does cyclic resolution for this definition
		return r.assemblers[pkgid].walkDef(def.SrcFile, def.Branch, def.DeclStatus)
	}

	// log all unresolved dependents
	for depname := range unresolved {
		r.logUnresolved(pkgid, def, def.Dependents[depname])
	}

	return false
}
