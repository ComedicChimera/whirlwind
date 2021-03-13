package resolve

import (
	"whirlwind/common"
	"whirlwind/syntax"
	"whirlwind/validate"
)

// PAssembler is responsible for putting the definitions in individual packages
// together and invoking the walker as necessary to generate their HIR.  It also
// stores the package-specific state for the resolver.
type PAssembler struct {
	SrcPackage *common.WhirlPackage

	// DefQueue is a queue of all definitions being resolved
	DefQueue *DefinitionQueue

	// walkers stores all the file-specific definition walkers for this package
	walkers map[*common.WhirlFile]*validate.Walker
}

// NewPAssembler creates a new package assembler for the given package
func NewPAssembler(srcpkg *common.WhirlPackage) *PAssembler {
	pa := &PAssembler{
		SrcPackage: srcpkg,
		DefQueue:   &DefinitionQueue{},
	}

	for fpath, wfile := range srcpkg.Files {
		pa.walkers[wfile] = validate.NewWalker(srcpkg, wfile, fpath, nil)
	}

	return pa
}

// initialPass performs stage 1 of the resolution algorithm.  It traverses each
// file in the package and extracts all determinate definitions that depend on
// unknown values.  All other definitions it resolves immediately. It also
// returns a value indicating whether or not the later stages of resolution need
// to occu based solely on its analysis of its package
func (pa *PAssembler) initialPass() bool {
	allResolved := true

	for wfile := range pa.walkers {
		// import processing should already have been run so the only things that
		// remain should be `top_level` and `export_block`
		for _, item := range wfile.AST.Content {
			block := item.(*syntax.ASTBranch)
			if block.Name == "export_block" {
				allResolved = allResolved && pa.initialPassOnBlock(wfile, block.BranchAt(2), common.DSExported)
			} else /* top_level */ {
				allResolved = allResolved && pa.initialPassOnBlock(wfile, block, common.DSInternal)
			}
		}
	}

	return allResolved
}

// initialPassOnBlock performs the initial pass on a single block
func (pa *PAssembler) initialPassOnBlock(wfile *common.WhirlFile, block *syntax.ASTBranch, declStatus int) bool {
	// all content of top level is `definition`
	for _, item := range block.Content {
		// get the internal definition node (eg. `type_def`)
		defNode := item.(*syntax.ASTBranch)
		defCore := defNode.BranchAt(0)

		// get the internal definition from an annotated def
		if defCore.Name == "annotated_def" {
			defCore = defCore.Last().(*syntax.ASTBranch)
		}

		// extract the name and dependents as necessary
		var name string
		var deps map[string]*DependentSymbol
		se := NewSymbolExtractor(pa.SrcPackage, wfile)
		if defCore.Name == "type_def" {
			name, deps = se.extractFromTypeDef(defCore)
		} else if defCore.Name == "interf_def" {
			name, deps = se.extractFromInterfDef(defCore)
		} else {
			// skip the definition if it is not determinate
			continue
		}

		// if there are no dependents, we can just walk the definition here
		if len(deps) == 0 {
			pa.walkDef(wfile, defNode, declStatus)
		} else {
			pa.DefQueue.Enqueue(&Definition{
				Name:       name,
				Branch:     defNode,
				Dependents: deps,
				SrcFile:    wfile,
				DeclStatus: declStatus,
			})
		}
	}

	return false
}

// walkDef walks a definition node in a specific file.  We don't actually return
// a flag here since we don't care for the purposes of resolution whether or not
// the definition was walked successfully
func (pa *PAssembler) walkDef(wfile *common.WhirlFile, defNode *syntax.ASTBranch, declStatus int) {
	hirn, _, _, ok := pa.walkers[wfile].WalkDef(defNode, declStatus)

	if ok {
		wfile.AddNode(hirn)
	}
}
