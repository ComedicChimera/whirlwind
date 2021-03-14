package resolve

import (
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
	Resolves map[uint][]string
}

// resolveCyclic runs stage 3 of the resolution algorithm: it attempts cyclic
// dependendency resolution on our top level definitions -- it is the "last
// resort" resolution pass.  It returns a boolean indicating whether all of the
// remaining type definitions will be resolved.  Regardless of this, all
// determinate definitions will be resolved by the time this function exits.
func (r *Resolver) resolveCyclic() bool {
	r.constructCyclicDefGraph()

	for len(r.cyclicGraph) > 0 {
		operand, operandSrcPkgID := r.getNextOperand()
		r.createOpaqueType(operand, operandSrcPkgID)

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
			pkggraph[def.Name] = &CyclicDefGraphNode{Def: def}
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
						// initialize an empty slice for this package in resolves
						ddnode.Resolves[pkgid] = nil
					}

					ddnode.Resolves[pkgid] = append(ddnode.Resolves[pkgid], name)
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
		for _, presolve := range presolves {
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
		newPresolves := presolves[:0]
		for _, name := range presolves {
			if r.cyclicLookup(pkgid, name) {
				newPresolves = append(newPresolves, name)
			}
		}

		if len(newPresolves) == 0 {
			delete(def.Resolves, pkgid)
		} else {
			def.Resolves[pkgid] = newPresolves
		}
	}
}

// cyclicLookup looks up a symbol during cyclic resolution and returns whether
// or not it found a match -- it operates on the graph and opaque symbol
func (r *Resolver) cyclicLookup(pkgid uint, name string) bool {
	if _, ok := r.cyclicGraph[pkgid][name]; ok {
		return true
	} else if r.sharedOpaqueSymbol.SrcPackageID == pkgid && r.sharedOpaqueSymbol.Name == name {
		return true
	}

	return false
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
		for _, name := range presolves {
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
