package resolve

import (
	"math"
	"whirlwind/common"
	"whirlwind/logging"
	"whirlwind/syntax"
	"whirlwind/typing"
)

// CyclicDefGraph is a graph of all the definitions that require cyclic
// definition resolution -- can't be resolved using the standard mechanism.  We
// choose only to create it here since it is a much larger data structure than
// the queue and and the performance cost of using it should be avoided if
// standard resolution is equally effective.  The key of the top-layer map is a
// package ID, the key of the lower layer is the name of the definition
type CyclicDefGraph map[uint]map[string]*CyclicDefGraphNode

// CyclicDefGraphNode is a type that is used to represent a node in the definition
// dependency graph that is created to resolve cyclic definitions.  It is a by
// directional node connoting both "depends on" and "depended on by" connections
type CyclicDefGraphNode struct {
	Def *Definition

	// Resolves stores a list all of the definitions that this one would resolve
	// if defined.  It denotes the "depended on by" relation.  These are
	// organized by package
	Resolves map[uint]map[string]struct{}
}

// resolveCyclic runs stage 3 of the resolution algorithm: it attempts cyclic
// dependendency resolution on our top level definitions -- it is the "last
// resort" resolution pass.  It returns a boolean indicating whether all of the
// remaining type definitions will be resolved.  Regardless of this, all
// determinate definitions will be resolved by the time this function exits.
func (r *Resolver) resolveCyclic() bool {
	r.constructCyclicDefGraph()

	// pick our initial operand
	operand, operandSrcPkgID := r.getNextOperand()
	for len(r.cyclicGraph) > 0 {
		// start by converting the operand to an opaque type
		r.createOpaqueType(operand, operandSrcPkgID)

		noneResolved := true
		for pkgid, presolves := range operand.Resolves {
			for presolve := range presolves {
				// attempts to recursively resolve the definition dependent upon
				// our operand
				if r.cyclicResolveDef(pkgid, r.cyclicGraph[pkgid][presolve]) {
					noneResolved = false
				}
			}
		}

		var nextOperand *CyclicDefGraphNode
		var nextOperandSrcPkgID uint
		if noneResolved {
			// since some definitions may have failed walking, we still need to
			// update the resolves before selecting our next operand
			r.updateResolves(operand)

		outerloop:
			for pkgid, presolves := range operand.Resolves {
				for presolve := range presolves {
					dnode := r.cyclicGraph[pkgid][presolve]

					if len(dnode.Resolves) > 0 {
						// also need to update the new operands resolves
						r.updateResolves(dnode)

						nextOperand = dnode
						nextOperandSrcPkgID = pkgid
						break outerloop
					}
				}
			}

			// none of the "resolves" of operand resolve anything (or still
			// exist), so we will need to choose a different operand -- just use
			// the regular operand selection algorithm for this
			if nextOperand == nil {
				nextOperand, nextOperandSrcPkgID = r.getNextOperand()
			}
		} else {
			nextOperand, nextOperandSrcPkgID = r.getNextOperand()
		}

		// always add the operand back in if it doesn't resolve and clear the
		// opaque symbol (reset)
		r.cyclicGraph[operandSrcPkgID][operand.Def.Name] = operand
		r.clearOpaqueSymbol()

		// attempt to resolve our operand -- always do this because our
		// graph may have changed and some definitions may have been removed
		// so the operand may no longer by valid
		r.cyclicResolveDef(operandSrcPkgID, operand)

		// set the next operand to be whatever one we determined
		operand = nextOperand
		operandSrcPkgID = nextOperandSrcPkgID
	}

	return logging.ShouldProceed()
}

// constructCyclicDefGraph constructs a CyclicDefGraph of all the remaining,
// unresolved definitions in every package
func (r *Resolver) constructCyclicDefGraph() {
	graph := make(CyclicDefGraph)

	// initialize the global graph up here so that we don't have to pass it to
	// prune definitions
	r.cyclicGraph = graph

	for pkgid, pa := range r.assemblers {
		pkggraph := make(map[string]*CyclicDefGraphNode)

		// dequeue everything in our package assembler definition queues and
		// move them into the table (as we no longer need the queues for
		// resolution so we can save space by clearing them)
		for pa.DefQueue.Len() > 0 {
			def := pa.DefQueue.Peek()

			pkggraph[def.Name] = &CyclicDefGraphNode{Def: def, Resolves: make(map[uint]map[string]struct{})}
			pa.DefQueue.Dequeue()
		}

		graph[pkgid] = pkggraph
	}

	// fill in our resolves field now that all of our definitions have been
	// moved into the graph -- filling in resolves requires each definition to
	// transfer their dependents to the definition that resolves them.  If a
	// definition depends on a symbol not in the graph, then we need to remove
	// it and log it as unresolved
	definitionsRemoved := false
	for pkgid, pkggraph := range graph {
		for dname, dnode := range pkggraph {
			for name, dep := range dnode.Def.Dependents {
				var depgraph map[string]*CyclicDefGraphNode
				if dep.ForeignPackage == nil {
					depgraph = pkggraph
				} else {
					// we can assume it is in the graph at this point since if
					// it isn't, it would have been removed during
					// `resolveStandard`
					depgraph = graph[dep.ForeignPackage.PackageID]
				}

				// if our definition exists, then we can just add resolves accordingly
				if ddnode, ok := depgraph[name]; ok {
					// we have to mutate the slice we can't extract it here
					if _, ok := ddnode.Resolves[pkgid]; !ok {
						// initialize an empty map for this package in resolves
						ddnode.Resolves[pkgid] = make(map[string]struct{})
					}

					ddnode.Resolves[pkgid][dname] = struct{}{}
				} else {
					// definition does not exist, prune the one we are currently using
					// and log the dependent symbol as undefined
					r.logUnresolved(pkgid, dnode.Def, dep)
					definitionsRemoved = true
					delete(pkggraph, dname)
				}

			}
		}
	}

	// If any definitions were removed, we may need to do repeated passes of the
	// graph to prune any definitions that are now themselves invalid.  We wait
	// to do recursive pruning until here since all our definitions may not have
	// their resolves fully filled out until now
	for definitionsRemoved {
		definitionsRemoved = false

		for pkgid, pkggraph := range graph {
			for _, dnode := range pkggraph {
				for _, dep := range dnode.Def.Dependents {
					var depgraph map[string]*CyclicDefGraphNode
					if dep.ForeignPackage == nil {
						depgraph = pkggraph
					} else {
						depgraph = graph[dep.ForeignPackage.PackageID]
					}

					// corresponding definition does not exist, so we prune
					if _, ok := depgraph[dep.Name]; !ok {
						definitionsRemoved = true
						r.logUnresolved(pkgid, dnode.Def, dep)

						r.pruneFromGraph(pkgid, dnode)
					}
				}
			}
		}
	}

}

// pruneFromGraph removes a definition from the cyclic definition graph and
// prunes away all its dependent definitions as well -- logging errors
// appropriately
func (r *Resolver) pruneFromGraph(pkgid uint, dnode *CyclicDefGraphNode) {
	delete(r.cyclicGraph[pkgid], dnode.Def.Name)

	for rpkgid, presolves := range dnode.Resolves {
		for presolve := range presolves {
			// resolve may have been deleted and the graph might not
			// be updated yet, so we need to check here before pruning
			if rdnode, ok := r.cyclicGraph[rpkgid][presolve]; ok {
				// our definition is now undefined so we should log accordingly
				r.logUnresolved(rpkgid, rdnode.Def, rdnode.Def.Dependents[dnode.Def.Name])

				// prune recursively
				r.pruneFromGraph(rpkgid, rdnode)
			}
		}
	}
}

// getNextOperand gets the next operand from the table.  It also updates the
// `Resolves` of any definition it hits along the way
func (r *Resolver) getNextOperand() (*CyclicDefGraphNode, uint) {
	for pkgid, pkggraph := range r.cyclicGraph {
		for name, dnode := range pkggraph {
			r.updateResolves(dnode)

			if len(dnode.Resolves) > 0 {
				// remove the operand from the graph before beginning resolution
				delete(pkggraph, name)
				return dnode, pkgid
			}
		}
	}

	// this should never be reachable logically
	logging.LogFatal("Located `nil` operand during cyclic resolution")
	return nil, 0
}

// updateResolves updates the `Resolves` field of a `CyclicDefGraphNode` to
// account for any definitions removed during cyclic resolution
func (r *Resolver) updateResolves(def *CyclicDefGraphNode) {
	for pkgid, presolves := range def.Resolves {
		for name := range presolves {
			if _, ok := r.cyclicGraph[pkgid][name]; !ok && !r.matchesOpaqueSymbol(pkgid, name) {
				delete(presolves, name)
			}
		}

		if len(presolves) == 0 {
			delete(def.Resolves, pkgid)
		}
	}
}

// createOpaqueType takes a definition and creates and stores an appropriate
// opaque type for it.  It stores it as the opaque type in all the walkers.
func (r *Resolver) createOpaqueType(operand *CyclicDefGraphNode, operandSrcPkgID uint) {
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

	switch operand.Def.Branch.LeafAt(0).Kind {
	case syntax.TYPE:
		generic = operand.Def.Branch.BranchAt(2).Name == "generic_tag"
		requiresRef = typeRequiresRef(operand.Def.Branch.Last().(*syntax.ASTBranch))
	case syntax.INTERF:
		if _, ok := operand.Def.Branch.Content[2].(*syntax.ASTBranch); ok {
			generic = ok
		}

		// interfaces never require a reference
	case syntax.CLOSED:
		generic = operand.Def.Branch.BranchAt(3).Name == "generic_tag"
		requiresRef = typeRequiresRef(operand.Def.Branch.Last().(*syntax.ASTBranch))
	}

	dependsOn := make(map[string][]uint)
	for pkgid, presolves := range operand.Resolves {
		for name := range presolves {
			if pkgids, ok := dependsOn[name]; ok {
				dependsOn[name] = append(pkgids, pkgid)
			} else {
				dependsOn[name] = []uint{pkgid}
			}
		}
	}

	var opaqueType typing.DataType
	if generic {
		opaqueType = &typing.OpaqueGenericType{}
	} else {
		opaqueType = &typing.OpaqueType{Name: operand.Def.Name}
	}

	*r.sharedOpaqueSymbol = common.OpaqueSymbol{
		Name:         operand.Def.Name,
		Type:         opaqueType,
		SrcPackageID: operandSrcPkgID,
		DependsOn:    dependsOn,
		RequiresRef:  requiresRef,
	}
}

// matchesOpaqueSymbol tests if a given name and package ID match the globally
// declared opaque symbol
func (r *Resolver) matchesOpaqueSymbol(pkgid uint, name string) bool {
	return r.sharedOpaqueSymbol.SrcPackageID == pkgid && r.sharedOpaqueSymbol.Name == name
}

// cyclicResolveDef attempts to resolve a cyclically defined type (like regular
// resolve but for cyclic resolution).  It also removes the cyclic definition
// from the graph if it resolves
func (r *Resolver) cyclicResolveDef(pkgid uint, dnode *CyclicDefGraphNode) bool {
	for _, dep := range dnode.Def.Dependents {
		// check first for an opaque symbol match
		if dep.ForeignPackage == nil {
			if r.matchesOpaqueSymbol(pkgid, dep.Name) {
				delete(dnode.Def.Dependents, dep.Name)
			}
		} else if r.matchesOpaqueSymbol(dep.ForeignPackage.PackageID, dep.Name) {
			delete(dnode.Def.Dependents, dep.Name)
		}

		// then, do a regular lookup within the definitions file for it
		if r.lookup(pkgid, dnode.Def.SrcFile, dep) {
			delete(dnode.Def.Dependents, dep.Name)
		}
	}

	// no dependents means we can resolve it
	if len(dnode.Def.Dependents) == 0 {
		// we don't know what other state has changed so we update the resolves
		// just to make sure we don't try to resolve something that doesn't
		// exist (especially since there are cycles)
		r.updateResolves(dnode)

		// we know the definition resolves so we can now just walk the
		// definition and handle the result
		if r.assemblers[pkgid].walkDef(dnode.Def.SrcFile, dnode.Def.Branch, dnode.Def.DeclStatus) {
			// remove it from the graph since it has been resolved (we don't do
			// this above since we want to prune if walking fails)
			delete(r.cyclicGraph[pkgid], dnode.Def.Name)

			// recursively resolve all its dependents
			for pkgid, presolves := range dnode.Resolves {
				for presolve := range presolves {
					// we don't care about the result here
					r.cyclicResolveDef(pkgid, r.cyclicGraph[pkgid][presolve])
				}
			}

			// this is the only codepath on which the symbol fully resolved properly
			return true
		} else {
			// some other error prevented the definition from resolving, meaning
			// it never will and we need to prune its dependencies accordingly
			r.pruneFromGraph(pkgid, dnode)
		}
	}

	return false
}

// clearOpaqueSymbol resets the opaque symbol so the next operand can be selected
func (r *Resolver) clearOpaqueSymbol() {
	*r.sharedOpaqueSymbol = common.OpaqueSymbol{Name: "", SrcPackageID: math.MaxUint32}
}
