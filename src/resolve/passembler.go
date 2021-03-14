package resolve

import (
	"fmt"
	"whirlwind/common"
	"whirlwind/logging"
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
	for wfile := range pa.walkers {
		// import processing should already have been run so the only things that
		// remain should be `top_level` and `export_block`
		for _, item := range wfile.AST.Content {
			block := item.(*syntax.ASTBranch)
			if block.Name == "export_block" {
				pa.initialPassOnBlock(wfile, block.BranchAt(2), common.DSExported)
			} else /* top_level */ {
				pa.initialPassOnBlock(wfile, block, common.DSInternal)
			}
		}
	}

	// if there are no definitions in the queue, then no definitions were
	// unresolved; otherwise, we have definitions to resolve
	return pa.DefQueue.Len() == 0
}

// initialPassOnBlock performs the initial pass on a single block
func (pa *PAssembler) initialPassOnBlock(wfile *common.WhirlFile, block *syntax.ASTBranch, declStatus int) {
	// all content of top level is `definition`
	for _, item := range block.Content {
		// get the internal definition node (eg. `type_def`)
		defCore := item.(*syntax.ASTBranch).BranchAt(0)

		// get the internal definition from an annotated def
		if defCore.Name == "annotated_def" {
			defCore = defCore.LastBranch()
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
			pa.walkDef(wfile, defCore, declStatus)
		} else {
			pa.DefQueue.Enqueue(&Definition{
				Name:       name,
				Branch:     defCore,
				Dependents: deps,
				SrcFile:    wfile,
				DeclStatus: declStatus,
			})
		}
	}
}

// finalPass walks all the indeterminate definitions in a package and resolves
// them if possible; it does not indicate whether or not all definitions
// resolved successfully.
func (pa *PAssembler) finalPass() {
	for wfile := range pa.walkers {
		for _, item := range wfile.AST.Content {
			block := item.(*syntax.ASTBranch)
			if block.Name == "export_block" {
				pa.finalPassOnBlock(wfile, block.BranchAt(2), common.DSExported)
			} else /* top_level */ {
				pa.finalPassOnBlock(wfile, block, common.DSInternal)
			}
		}
	}
}

// finalPassOnBlock performs the final resolution pass on block content
func (pa *PAssembler) finalPassOnBlock(wfile *common.WhirlFile, block *syntax.ASTBranch, declStatus int) {
	// all content of `top_level` is `definition`
	for _, item := range block.Content {
		// extract the internal definition node
		defCore := item.(*syntax.ASTBranch).BranchAt(0)

		switch defCore.Name {
		case "type_def", "interf_def":
			// already processed
			continue
		case "annotated_def":
			defCore = defCore.LastBranch()

			if defCore.Name == "type_def" || defCore.Name == "interf_def" {
				// already processed
				continue
			}

			fallthrough
		default:
			// all indeterminate definitions end up here
			pa.walkDef(wfile, defCore, declStatus)
		}
	}
}

// checkImports checks if all the explicitly imported symbols of this package resolved
func (pa *PAssembler) checkImports() {
	for _, walker := range pa.walkers {
		for name, wsi := range walker.SrcFile.LocalTable {
			if wsi.SymbolRef.Name == "" {
				// if it is still empty here, then it may be unresolveable
				// or it may simply have never been used.  We test to
				// determine both cases
				if isym, ok := wsi.SrcPackage.ImportFromNamespace(name); ok {
					*wsi.SymbolRef = *isym

					// warn that the symbol is unused
					logging.LogWarning(
						walker.Context,
						fmt.Sprintf("Symbol `%s` imported but never used", name),
						logging.LMKName,
						wsi.Position,
					)
				} else {
					// symbol is actually undefined
					walker.LogNotVisibleInPackage(name, wsi.SrcPackage.Name, wsi.Position)
				}
			}
		}
	}
}

// walkDef walks a definition node in a specific file.  We don't actually return
// a flag here since we don't care for the purposes of resolution whether or not
// the definition was walked successfully
func (pa *PAssembler) walkDef(wfile *common.WhirlFile, defNode *syntax.ASTBranch, declStatus int) {
	hirn, ok := pa.walkers[wfile].WalkDef(defNode, declStatus)

	if ok {
		wfile.AddNode(hirn)
	}
}
