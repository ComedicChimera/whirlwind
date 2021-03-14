package resolve

import (
	"whirlwind/common"
	"whirlwind/logging"
	"whirlwind/syntax"
)

// DependentSymbol is a symbol awaiting resolution that is depended upon by a
// definition that is being resolved
type DependentSymbol struct {
	Name     string
	Position *logging.TextPosition

	// ForeignPackage is the location where this symbol is expected to be found.
	// This field is nil if this symbol belongs to the current package
	ForeignPackage *common.WhirlPackage

	// ImplicitImport is used to indicate whether or not a symbol is implicitly
	// imported. This field is meaningless if the ForeignPackage field is nil.
	ImplicitImport bool
}

// SymbolExtractor walks a single definition and determines all its dependent
// symbols as it runs.  It does not determine whether or not any of the
// dependent symbols are actually defined globally -- it does not perform any
// lookups.  It is a small type that mainly functions to store state between
// recursive extraction calls
type SymbolExtractor struct {
	srcpkg *common.WhirlPackage
	wfile  *common.WhirlFile

	// knowns stores a list of all symbols that are defined as apart of the
	// definition itself (eg. generic type parameters, self-references, etc.)
	knowns map[string]struct{}

	// localKnowns stores the knowns of a sub-definition of a larger definition
	// (like a method or method specialization)
	localKnowns map[string]struct{}

	// dependents stores a list of all dependent symbols that are being
	// accumulated through extraction.  This is returned at the end of
	// extraction
	dependents map[string]*DependentSymbol
}

// NewSymbolExtractor creates a new symbol extractor for any given definition.
// It initializes a symbol extractor for extraction
func NewSymbolExtractor(srcpkg *common.WhirlPackage, wfile *common.WhirlFile) *SymbolExtractor {
	return &SymbolExtractor{
		srcpkg:      srcpkg,
		wfile:       wfile,
		knowns:      make(map[string]struct{}),
		localKnowns: make(map[string]struct{}),
		dependents:  make(map[string]*DependentSymbol),
	}
}

// ExtractFromTypeDef extracts dependent symbols for a type definition.  It also
// returns the name of the type being defined
func (se *SymbolExtractor) extractFromTypeDef(typedef *syntax.ASTBranch) (string, map[string]*DependentSymbol) {
	var name string

	for _, item := range typedef.Content {
		switch v := item.(type) {
		case *syntax.ASTLeaf:
			if v.Kind == syntax.IDENTIFIER {
				name = v.Value

				// self-type declaration
				se.knowns[name] = struct{}{}
			}
		case *syntax.ASTBranch:
			switch v.Name {
			case "generic_tag":
				se.extractFromGenericTag(v, false)
			case "typeset":
				se.extractFromTypeList(v)
			case "newtype":
				se.extractAllFromBranch(v)
			}
		}
	}

	return name, se.dependents
}

// ExtractFromInterfDef extracts dependent symbols from an interface definition.
// It also returns the name of the interface being defined
func (se *SymbolExtractor) extractFromInterfDef(interfdef *syntax.ASTBranch) (string, map[string]*DependentSymbol) {
	var name string

	for _, item := range interfdef.Content {
		switch v := item.(type) {
		case *syntax.ASTLeaf:
			if v.Kind == syntax.IDENTIFIER {
				name = v.Value

				// self-type declaration
				se.knowns[name] = struct{}{}
			}
		case *syntax.ASTBranch:
			switch v.Name {
			case "generic_tag":
				se.extractFromGenericTag(v, false)
			case "interf_body":
				se.extractFromInterfBody(v)
			}
		}
	}

	return name, se.dependents
}

// extractFromInterfBody extracts the dependent symbols from an `interf_body`
func (se *SymbolExtractor) extractFromInterfBody(body *syntax.ASTBranch) {
	for _, item := range body.Content {
		// only possible branches are `interf_member`
		if member, ok := item.(*syntax.ASTBranch); ok {
			// get the internal member node (strip away `interf_member`)
			memberCore := member.BranchAt(0)

			switch memberCore.Name {
			case "func_def":
				se.extractFromMethod(memberCore)
			case "annotated_method":
				se.extractFromMethod(memberCore.LastBranch())
			case "special_def":
				// generic tags and specialization specifiers can still create
				// dependencies (eg. `<T: Name>``)
				for _, elem := range memberCore.Content {
					if elembranch, ok := elem.(*syntax.ASTBranch); ok {
						switch elembranch.Name {
						case "generic_tag":
							se.extractFromGenericTag(elembranch, true)
						case "type_list":
							se.extractFromTypeList(elembranch)
						}
					}
				}

				// clear our local knowns
				se.localKnowns = make(map[string]struct{})
			}
		}
	}
}

// extractFromMethod extracts from a `func_def` node found in an `interf_body`
func (se *SymbolExtractor) extractFromMethod(method *syntax.ASTBranch) {
	for _, item := range method.Content {
		if itembranch, ok := item.(*syntax.ASTBranch); ok {
			switch itembranch.Name {
			case "generic_tag":
				se.extractFromGenericTag(itembranch, true)
			case "signature":
				// signature just contains a bunch of type labels are other misc
				// stuff so we can just reuse this function here
				se.extractAllFromBranch(itembranch)
			}
		}
	}

	// clear our local knowns
	se.localKnowns = make(map[string]struct{})
}

// extractAllFromBranch recursively extracts the dependent symbols from a
// definition branch. It can be called with any "sub-node" of a definition to
// obtain its dependents -- this function basically extracts all the type labels
// (skipping initializers for obvious reasons)
func (se *SymbolExtractor) extractAllFromBranch(branch *syntax.ASTBranch) {
	for _, item := range branch.Content {
		if subbranch, ok := item.(*syntax.ASTBranch); ok {
			switch subbranch.Name {
			case "type":
				se.extractFromTypeLabel(subbranch)
			case "initializer":
				// skip over initializers so we don't start extracting
				// dependents from them (no expr dependencies)
				continue
			default:
				// begin recursive extraction
				se.extractAllFromBranch(subbranch)
			}
		}
	}
}

// extractFromGenericTag extracts the dependent symbols from a `generic_tag`
func (se *SymbolExtractor) extractFromGenericTag(genericTag *syntax.ASTBranch, local bool) {
	for _, item := range genericTag.Content {
		// only possible branch is `genericParam`
		if genericParam, ok := item.(*syntax.ASTBranch); ok {
			for _, elem := range genericParam.Content {
				switch v := elem.(type) {
				case *syntax.ASTBranch:
					// only possible branch is `type`
					se.extractFromTypeLabel(v)
				case *syntax.ASTLeaf:
					// the leaves are the type param names
					if local {
						se.localKnowns[v.Value] = struct{}{}
					} else {
						se.knowns[v.Value] = struct{}{}
					}
				}
			}
		}
	}
}

// extractFromTypeList extracts the dependent symbols from any branch that is
// just a list of tokens and type labels
func (se *SymbolExtractor) extractFromTypeList(typeList *syntax.ASTBranch) {
	for _, item := range typeList.Content {
		// only possible branch is `type`
		if typeLabel, ok := item.(*syntax.ASTBranch); ok {
			se.extractFromTypeLabel(typeLabel)
		}
	}
}

// extractFromTypeLabel extracts the dependent symbols from a `type`
func (se *SymbolExtractor) extractFromTypeLabel(label *syntax.ASTBranch) {
	labelCore := label.BranchAt(0)
	se.extractFromTypeLabelCore(labelCore)
}

// extractFromTypeLabelCore extracts dependents from the internals of a type
// label (eg. `value_type` not just `type`) -- made into a function for reuse in
// things like reference types
func (se *SymbolExtractor) extractFromTypeLabelCore(labelCore *syntax.ASTBranch) {
	switch labelCore.Name {
	// `prim_type` and `region_type` will never contain dependencies so we can
	// ignore them as nodes here
	case "named_type":
		// could be an implicit import or a regular old identifier: have to
		// handle both cases (rootName is before `::`, accessedName is after)
		var rootName, accessedName string
		var rootPos, accessedPos *logging.TextPosition
		for _, item := range labelCore.Content {
			switch v := item.(type) {
			case *syntax.ASTBranch:
				// only branch is a `type_list`
				se.extractFromTypeList(v)
			case *syntax.ASTLeaf:
				if v.Kind == syntax.IDENTIFIER {
					// since they occur in sequential order, this should always
					// work to distinguish the two names
					if rootName == "" {
						rootName = v.Value
						rootPos = v.Position()
					} else {
						accessedName = v.Value
						accessedPos = v.Position()
					}
				}
			}
		}

		// no accessed name, just regular, old identifier
		if accessedName == "" {
			se.addDependent(rootName, rootPos)
		} else {
			for _, wimport := range se.srcpkg.ImportTable {
				if wimport.PackageRef.Name == accessedName {
					se.dependents[accessedName] = &DependentSymbol{
						Name:           accessedName,
						Position:       accessedPos,
						ForeignPackage: wimport.PackageRef,
						ImplicitImport: true,
					}

					// all other dependents of this name type have been handled
					// so we can just return early here
					return
				}
			}

			// no matching import found, just ignore this dependency (as it will
			// never resolve and we can just handle this issue later down the
			// line)
		}
	case "value_type":
		valueTypeCore := labelCore.BranchAt(0)
		switch valueTypeCore.Name {
		case "func_type":
			// we can just extractAll again here since it is just a bunch of junk
			// with type labels buried in it
			se.extractAllFromBranch(valueTypeCore)
		case "tup_type", "col_type":
			// both of theses are just one level deep filled with type labels
			se.extractFromTypeList(valueTypeCore)
		case "vec_type":
			// if `vec_type` contains an identifier, it could have a dependency
			for _, item := range valueTypeCore.Content {
				if itemleaf, ok := item.(*syntax.ASTLeaf); ok {
					if itemleaf.Kind == syntax.IDENTIFIER {
						se.addDependent(itemleaf.Value, itemleaf.Position())
					}
				}
			}
		}
	case "ref_type":
		se.extractFromRefType(labelCore)
	}
}

// extractFromRefType extracts the contents of a ref type or any subnode of a
// `ref_type` (like extractAll but for `ref_type`)
func (se *SymbolExtractor) extractFromRefType(refType *syntax.ASTBranch) {
	for _, item := range refType.Content {
		if itembranch, ok := item.(*syntax.ASTBranch); ok {
			switch itembranch.Name {
			case "value_type", "named_type", "ref_type":
				se.extractFromTypeLabelCore(itembranch)
			default:
				se.extractFromRefType(itembranch)
			}
		}
	}
}

// addDependent determines whether or not a given name is a dependent symbol or
// not and if it is, adds to the dependent symbol map
func (se *SymbolExtractor) addDependent(name string, pos *logging.TextPosition) {
	if _, ok := se.knowns[name]; ok {
		return
	}

	if _, ok := se.localKnowns[name]; ok {
		return
	}

	// if this depends on an imported symbol, we need to create a different
	// dependent (has `ForeignPackage`)
	if localSym, ok := se.wfile.LocalTable[name]; ok {
		se.dependents[name] = &DependentSymbol{
			Name:           name,
			Position:       pos,
			ForeignPackage: localSym.SrcPackage,
		}
	} else {
		// just a regular dependent
		se.dependents[name] = &DependentSymbol{
			Name:     name,
			Position: pos,
		}
	}
}
