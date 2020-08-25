package analysis

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// walkDefinitions walks a `top_level` node and walks definitions
func (w *Walker) walkDefinitions(branch *syntax.ASTBranch) bool {
	for _, item := range branch.Content {
		// definition => `def_member`
		defNode := item.(*syntax.ASTBranch).BranchAt(0)

		switch defNode.Name {
		case "type_def":
			if !w.walkTypeDef(defNode) {
				return false
			}
		case "interf_def":
			if !w.walkInterfDef(defNode) {
				return false
			}
		case "interf_bind":
		case "operator_def":
			if !w.walkOperatorOverload(defNode) {
				return false
			}
		case "func_def":
			if fnode, ok := w.walkFuncDef(defNode); ok {
				if !w.defineFunc(fnode, defNode.Content[1].Position()) {
					return false
				}
			} else {
				return false
			}
		case "decorator":
		case "annotated_def":
			if !w.walkAnnotatedDef(defNode) {
				return false
			}
		case "variable_decl":
			if vdecl, ok := w.walkVarDecl(defNode); ok {
				w.Root.Elements = append(w.Root.Elements, vdecl)
			} else {
				return false
			}
		case "variant_def":
			if vnode, ok := w.walkVariantDef(defNode, func(name string) (types.DataType, bool) {
				if sym := w.Lookup(name); sym != nil {
					return sym.Type, true
				} else {
					return nil, false
				}
			}); ok {
				rg := vnode.(*common.HIRVariantDef).RootGeneric
				rg.Variants = append(rg.Variants, len(w.Root.Elements))
				w.Root.Elements = append(w.Root.Elements, vnode)
			} else {
				return false
			}
		}
	}

	return true
}

// walkTypeDef walks a `type_def` node
func (w *Walker) walkTypeDef(branch *syntax.ASTBranch) bool {
	// create our type symbol and type def node
	typeSym := &common.Symbol{
		DeclStatus: w.DeclStatus,
		DefKind:    common.SKindTypeDef,
		Constant:   true,
	}

	tdefNode := &common.HIRTypeDef{Sym: typeSym}

	// set up the state elements of the type def
	closed := false

	// collect positional data
	var typeSymPos *util.TextPosition
	var enumMemberPositions map[string]*util.TextPosition

	// collect information about the type definition
	for _, item := range branch.Content {
		switch v := item.(type) {
		case *syntax.ASTBranch:
			switch v.Name {
			case "generic_tag":
				if !w.setupGenericContext(v) {
					return false
				}
			case "typeset":
				ts := &types.TypeSet{
					Name:    typeSym.Name,
					SetKind: types.TSKindUnion,
				}

				for _, elem := range v.Content {
					// only branch is a type
					if tb, ok := elem.(*syntax.ASTBranch); ok {
						if dt, ok := w.walkTypeLabel(tb); ok {
							ts.NewTypeSetMember(dt)
						} else {
							return false
						}
					}
				}

				typeSym.Type = ts
			case "newtype":
				if nt, ok := w.walkNewType(v, tdefNode, enumMemberPositions); ok {
					typeSym.Type = nt
				} else {
					return false
				}
			}
		case *syntax.ASTLeaf:
			switch v.Kind {
			case syntax.CLOSED:
				closed = true
			case syntax.IDENTIFIER:
				typeSym.Name = v.Value
				typeSymPos = v.Position()
			}
		}
	}

	// generate and declare the type definition
	if !closed {
		if ts, ok := typeSym.Type.(*types.TypeSet); ok {
			if ts.SetKind == types.TSKindAlgebraic || ts.SetKind == types.TSKindEnum {
				for _, member := range ts.Members {
					emname := member.(*types.EnumMember).Name

					if !w.Define(&common.Symbol{
						Name:       emname,
						Type:       member,
						Constant:   true,
						DeclStatus: w.DeclStatus,
						DefKind:    common.SKindTypeDef,
					}) {
						ThrowMultiDefError(emname, enumMemberPositions[emname])
						return false
					}
				}
			}
		}
	}

	return w.finishDefinition(typeSym, tdefNode, typeSymPos)
}

// walkNewType walks a `newtype` node.  (tdn = type def node)
func (w *Walker) walkNewType(branch *syntax.ASTBranch, tdn *common.HIRTypeDef, memberPositions map[string]*util.TextPosition) (types.DataType, bool) {
	switch branch.Name {
	case "enum_suffix":
		ts := types.NewTypeSet(
			tdn.Sym.Name,
			types.TSKindEnum,
			nil,
		)

		for _, item := range branch.Content {
			mnode := item.(*syntax.ASTBranch)
			mname := mnode.LeafAt(1).Value
			memberPositions[mname] = mnode.Position()

			if mnode.Len() == 2 {
				if ts.NewEnumMember(mname, nil) {
					continue
				}
			} else if typeList, ok := w.walkTypeList(mnode.BranchAt(2).BranchAt(1)); ok {
				if ts.NewEnumMember(
					mname,
					typeList,
				) {
					ts.SetKind = types.TSKindAlgebraic
					continue
				}
			}

			util.ThrowError(
				fmt.Sprintf("Multiple type members with the same name: `%s`", mname),
				"Name",
				mnode.Position(),
			)
			return nil, false
		}

		return ts, true
	case "struct_suffix":
		members := make(map[string]*types.StructMember)
		var deriving *types.StructType

		for _, item := range branch.Content {
			if subbranch, ok := item.(*syntax.ASTBranch); ok {
				if subbranch.Name == "struct_member" {
					var names []string
					var constant, volatile bool

				memberloop:
					for i, elem := range subbranch.Content {
						switch v := elem.(type) {
						case *syntax.ASTBranch:
							if v.Name == "identifier_list" {
								names = namesFromIDList(v)
							} else {
								// next logical item is `type_ext`
								var dt types.DataType
								if wdt, ok := w.walkTypeExt(v); ok {
									dt = wdt
								} else {
									return nil, false
								}

								for _, name := range names {
									if _, ok := members[name]; ok {
										util.ThrowError(
											"Each struct member must have a unique name",
											"Name",
											subbranch.BranchAt(0).Content[i*2].Position(),
										)

										return nil, false
									}

									if initNode, ok := subbranch.Content[i+1].(*syntax.ASTBranch); ok {
										tdn.FieldInits[name] = (*common.HIRIncomplete)(initNode.BranchAt(1))
									}

									members[name] = &types.StructMember{
										Constant: constant, Volatile: volatile, Type: dt,
									}
								}

								break memberloop
							}
						case *syntax.ASTLeaf:
							if v.Kind == syntax.CONST {
								constant = true
							} else {
								// only other token that reaches this far
								volatile = true
							}
						}
					}
				} else if dt, ok := w.walkTypeLabel(subbranch); ok /* subbranch == "type" */ {
					if st, ok := dt.(*types.StructType); ok {
						deriving = st
					} else {
						util.ThrowError(
							"Structured types can only derive from other structured types",
							"Type",
							subbranch.Position(),
						)
					}
				}

			}
		}

		var packed bool
		if _, ok := w.CtxAnnotations["packed"]; ok {
			packed = true
		}

		return types.NewStructType(tdn.Sym.Name, members, packed, deriving), true
	}

	return nil, false
}

// walkInterfDef walks an `interf_def` node
func (w *Walker) walkInterfDef(branch *syntax.ASTBranch) bool {
	// create the base interface type -- no self type rules needed here
	tInterf := &types.TypeInterf{
		Methods: make(map[string]*types.Method),
	}

	name := branch.LeafAt(1).Value
	interfSym := &common.Symbol{
		Name:       name,
		Constant:   true,
		DefKind:    common.SKindTypeDef,
		DeclStatus: w.DeclStatus,
		Type:       types.NewInterface(name, tInterf),
	}

	interfNode := &common.HIRInterfDef{
		Sym: interfSym,
	}

	// if the node at position 2 is a Branch, then we know it is a `generic_tag`
	if b, ok := branch.Content[2].(*syntax.ASTBranch); ok {
		if !w.setupGenericContext(b) {
			return false
		}
	}

	// extract the methods of the interface
	if w.walkInterfBody(branch.Last().(*syntax.ASTBranch), tInterf, false, func(m common.HIRNode) int {
		interfNode.Methods = append(interfNode.Methods, m)
		return len(interfNode.Methods) - 1
	}) {
		return false
	}

	return w.finishDefinition(interfSym, interfNode, branch.Content[1].Position())
}

// walkInterfBind walks an `interf_bind` node
func (w *Walker) walkInterfBind(branch *syntax.ASTBranch) bool {
	// TODO: generic interface binding

	return false
}

// walkInterfBody walks an `interf_body` node and adds the methods it finds to
// the method map it is passed.  DOES NOT PARSE THEIR BODIES.  `addMethodNode`
// accepts a node to add and returns the position at which it was added.
func (w *Walker) walkInterfBody(branch *syntax.ASTBranch, tInterf *types.TypeInterf, isBinding bool, addMethodNode func(common.HIRNode) int) bool {
	for _, item := range branch.Content {
		// only branch in `interf_body` is `interf_member`
		if imemberBranch, ok := item.(*syntax.ASTBranch); ok {
			imember := imemberBranch.BranchAt(0)

			switch imember.Name {
			case "func_def":
				if defnode, ok := w.walkFuncDef(imember); ok {
					var fnNode *common.HIRFuncDef
					namePos := imember.LeafAt(1).Position()

					switch v := defnode.(type) {
					case *common.HIRFuncDef:
						fnNode = v
					case *common.HIRGeneric:
						fnNode = v.GenericNode.(*common.HIRFuncDef)
					}

					var mk int
					// TODO: figure out how to get the method kind

					if !tInterf.AddMethod(fnNode.Sym.Name, fnNode.Sym.Type, mk) {
						util.ThrowError(
							fmt.Sprintf("Method defined multiple times: `%s`", fnNode.Sym.Name),
							"Name",
							namePos,
						)

						return false
					}

					addMethodNode(defnode)
				} else {
					return false
				}
			case "variant_def":
				if vnode, ok := w.walkVariantDef(imember, func(name string) (types.DataType, bool) {
					if method, ok := tInterf.Methods[name]; ok {
						return method.FnType, true
					} else {
						return nil, false
					}
				}); ok {
					rg := vnode.(*common.HIRVariantDef).RootGeneric
					rg.Variants = append(rg.Variants, addMethodNode(vnode))

				} else {
					return false
				}
			}
		}
	}

	return true
}

// walkFuncDef walks a `func_def` node and extracts a HIRFuncDef from it.
// It does NOT declare the function's symbol nor does it add the HIRFuncDef to
// anything (allows this function to be used a number of places).  It may also
// produce a generic as necessary.
func (w *Walker) walkFuncDef(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	_, boxable := w.CtxAnnotations["intrinsic"]
	ft := &types.FuncType{Boxable: boxable, ConstStatus: types.CSUnknown}
	var name string
	var funcBody common.HIRNode
	var argData map[string]*common.HIRArgData

	for _, item := range branch.Content {
		switch v := item.(type) {
		case *syntax.ASTBranch:
			switch v.Name {
			case "generic_tag":
				if !w.setupGenericContext(v) {
					return nil, false
				}
			case "signature":
				if amap, ok := w.walkFuncSignature(v, ft, false); ok {
					argData = amap
				} else {
					return nil, false
				}
			case "func_body":
				if l, ok := v.Content[0].(*syntax.ASTLeaf); ok && l.Kind == syntax.CONST {
					ft.ConstStatus = types.CSConstant
				}

				funcBody = (*common.HIRIncomplete)(v)
			}
		case *syntax.ASTLeaf:
			switch v.Kind {
			case syntax.ASYNC:
				ft.Async = true
			case syntax.IDENTIFIER:
				name = v.Value
			}
		}
	}

	if ft.ReturnType == nil {
		ft.ReturnType = types.PrimitiveType(types.PrimNothing)
	}

	// we don't need to copy annotations since CtxAnnotations is cleared without
	// manipulating the underlying reference (and creates a new one on each
	// clear).
	fnode := &common.HIRFuncDef{
		Sym: &common.Symbol{
			Name:       name,
			Type:       ft,
			Constant:   true,
			DefKind:    common.SKindFuncDef,
			DeclStatus: w.DeclStatus,
		},
		Annotations: w.CtxAnnotations,
		Body:        funcBody,
		ArgData:     argData,
	}

	// use a dummy symbol for sake of ease :)
	return w.makeGeneric(&common.Symbol{Type: ft}, fnode), true
}

// `walkOperatorOverload` walks an `operator_overload` node and adds the
// overload to the overload table.
func (w *Walker) walkOperatorOverload(branch *syntax.ASTBranch) bool {
	opBranch := branch.BranchAt(2)
	opKind := opBranch.Content[util.BoolToInt(opBranch.Len() < 3)].(*syntax.ASTLeaf).Kind

	ft := &types.FuncType{}
	var argData map[string]*common.HIRArgData
	var funcBody common.HIRNode

	for _, node := range branch.Content[4:] {
		b := node.(*syntax.ASTBranch)

		switch b.Name {
		case "generic_tag":
			if !w.setupGenericContext(b) {
				return false
			}
		case "signature":
			if adata, ok := w.walkFuncSignature(b, ft, true); ok {
				argData = adata
			} else {
				return false
			}
		case "func_body":
			funcBody = (*common.HIRIncomplete)(b)
		}
	}

	switch opKind {
	case syntax.MINUS:
		if len(ft.Params) < 1 || len(ft.Params) > 2 {
			util.ThrowError(
				"Operator overload for minus operator must take either 1 or 2 arguments",
				"Signature",
				opBranch.Position(),
			)

			return false
		}
	case syntax.NOT, syntax.COMPL:
		if len(ft.Params) != 1 {
			util.ThrowError(
				"Operator overload for unary operator must take exactly 1 argument",
				"Signature",
				opBranch.Position(),
			)

			return false
		}
	case syntax.COLON:
		if len(ft.Params) != 3 {
			util.ThrowError(
				"Operator overload for slice operator must take exactly 3 arguments",
				"Signature",
				opBranch.Position(),
			)

			return false
		}
	// all other operators are pure binary
	default:
		if len(ft.Params) != 2 {
			util.ThrowError(
				"Operator overload for binary operator must take exactly 2 arguments",
				"Signature",
				opBranch.Position(),
			)

			return false
		}
	}

	var operDecl common.HIRNode = &common.HIROperDecl{
		OperKind:    opKind,
		Signature:   ft,
		Annotations: w.CtxAnnotations,
		Body:        funcBody,
		ArgData:     argData,
	}
	dummySym := &common.Symbol{Type: ft}
	operDecl = w.makeGeneric(dummySym, operDecl)

	if w.addOperatorOverload(opKind, dummySym.Type) {
		w.Root.Elements = append(w.Root.Elements, operDecl)
		return true
	}

	return false
}

// walkFuncSignature walks a `signature` node of any given function.  It accepts
// a base func-type as input containing everything that the signature doesn't.
func (w *Walker) walkFuncSignature(branch *syntax.ASTBranch, ft *types.FuncType, operator bool) (map[string]*common.HIRArgData, bool) {
	initMap := make(map[string]*common.HIRArgData)

	for _, item := range branch.Content {
		subbranch := item.(*syntax.ASTBranch)

		if subbranch.Name == "args_decl" {
			for _, argNode := range subbranch.Content {
				if b, ok := argNode.(*syntax.ASTBranch); ok {
					if b.Name == "arg_decl" {
						var names []string
						var argDt types.DataType
						var constant bool
						argData := &common.HIRArgData{}

						for _, elem := range b.Content {
							switch n := elem.(type) {
							case *syntax.ASTBranch:
								switch n.Name {
								case "identifier_list":
									names = namesFromIDList(n)

									for i, name := range names {
										if _, ok := initMap[name]; ok {
											util.ThrowError(
												fmt.Sprintf("Multiple arguments with name: `%s`", name),
												"Name",
												n.Content[i*2].Position(),
											)

											return nil, false
										}

										initMap[name] = argData
									}
								case "type_ext":
									if dt, ok := w.walkTypeLabel(n); ok {
										argDt = dt
									} else {
										return nil, false
									}
								case "initializer":
									if operator {
										util.ThrowError(
											"Operator overload can't take optional arguments",
											"Signature",
											n.Position(),
										)

										return nil, false
									}

									argData.Initializer = (*common.HIRIncomplete)(n)
								}
							case *syntax.ASTLeaf:
								if n.Kind == syntax.CONST {
									constant = true
								} else {
									argData.Volatile = true
								}
							}
						}

						for _, name := range names {
							ft.Params = append(ft.Params, &types.FuncParam{
								Name:     name,
								Type:     argDt,
								Optional: argData.Initializer != nil,
								Constant: constant,
							})
						}
					} else if operator {
						util.ThrowError(
							"Operator overloads can't take variadic arguments",
							"Signature",
							b.Position(),
						)

						return nil, false
					} else /* `var_arg_decl` */ {
						name := b.LeafAt(1).Value

						if _, ok := initMap[name]; ok {
							util.ThrowError(
								fmt.Sprintf("Multiple arguments with name: `%s`", name),
								"Name",
								b.Content[1].Position(),
							)

							return nil, false
						}

						if dt, ok := w.walkTypeLabel(b.BranchAt(2)); ok {
							ft.Params = append(ft.Params, &types.FuncParam{
								Name:     name,
								Type:     dt,
								Variadic: true,
								Optional: true, // marked optional for convenience
								Constant: false,
							})
						} else {
							return nil, false
						}
					}
				}
			}
		} else if rt, ok := w.walkTypeLabel(subbranch); ok {
			ft.ReturnType = rt
		} else {
			return nil, false
		}
	}

	return initMap, true
}

// walkVariantDef walks a `variant_def` node and returns a HIRNode instead of declaring
// anything (so as to work w/ methods -- same behavior as `walkFuncDef`)
func (w *Walker) walkVariantDef(branch *syntax.ASTBranch, lookupGeneric func(string) (types.DataType, bool)) (common.HIRNode, bool) {
	typeList, ok := w.walkTypeList(branch.BranchAt(2))

	if !ok {
		return nil, false
	}

	idLeaf := branch.LeafAt(4)
	dt, ok := lookupGeneric(idLeaf.Value)
	if !ok {
		ThrowUndefinedError(idLeaf.Value, idLeaf.Position())
		return nil, false
	}

	if gt, ok := dt.(*types.GenericType); ok {
		return &common.HIRVariantDef{
			RootGeneric: gt,
			TypeParams:  typeList,
			Body:        (*common.HIRIncomplete)(branch.BranchAt(5)),
		}, true
	} else {
		util.ThrowError(
			fmt.Sprintf("Variant symbol `%s` must be a generic type", idLeaf.Value),
			"Type",
			idLeaf.Position(),
		)

		return nil, false
	}
}

// walkAnnotatedDef walks an `annotated_def` or an `annotated_method` node.
func (w *Walker) walkAnnotatedDef(branch *syntax.ASTBranch) bool {
	for i, item := range branch.Content {
		subbranch := item.(*syntax.ASTBranch)

		if subbranch.Name == "annotation" {
			// TODO: should bad annotations throw errors?

			if subbranch.Len() == 3 {
				w.CtxAnnotations[subbranch.LeafAt(1).Value] = ""
			} else {
				w.CtxAnnotations[subbranch.LeafAt(1).Value] = subbranch.LeafAt(2).Value
			}
		} else {
			// we use walk definitions to handle our annotation body by simply
			// trimming off the `annotation` and giving it a container node with
			// length of one that holds the definition we want to analyze/walk.
			result := w.walkDefinitions(&syntax.ASTBranch{Name: "annot_body", Content: branch.Content[i:]})

			w.CtxAnnotations = make(map[string]string)

			return result
		}
	}

	// unreachable
	return false
}

// finishDefinition is used to globally declare a definition and add its node to
// the HIRRoot if possible.  It also handles all generics involved in the
// definition.
func (w *Walker) finishDefinition(sym *common.Symbol, node common.HIRNode, namePos *util.TextPosition) bool {
	if w.shouldCreateGeneric() {
		node = w.makeGeneric(sym, node)
	}

	if !w.Define(sym) {
		ThrowMultiDefError(sym.Name, namePos)
		return false
	}

	w.Root.Elements = append(w.Root.Elements, node)
	return true
}

// defineFunc defines a generic or non-generic function locally or globally
func (w *Walker) defineFunc(fnode common.HIRNode, namePos *util.TextPosition) bool {
	var sym *common.Symbol
	switch v := fnode.(type) {
	case *common.HIRFuncDef:
		sym = v.Sym
	case *common.HIRGeneric:
		*sym = *v.GenericNode.(*common.HIRFuncDef).Sym
		sym.Type = v.Generic
	}

	if !w.Define(sym) {
		ThrowMultiDefError(sym.Name, namePos)
		return false
	}

	w.Root.Elements = append(w.Root.Elements, fnode)
	return true
}
