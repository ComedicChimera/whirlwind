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
		case "func_def":
		case "decorator":
		case "annotated_def":
			if !w.walkAnnotatedDef(defNode) {
				return false
			}
		case "variable_decl":
		case "variant_def":
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
	genericParams := make(map[string]*types.TypeParam)

	// collect positional data
	var typeSymPos *util.TextPosition
	var enumMemberPositions map[string]*util.TextPosition

	// collect information about the type definition
	for _, item := range branch.Content {
		switch v := item.(type) {
		case *syntax.ASTBranch:
			switch v.Name {
			case "generic_tag":
				if gp, ok := w.walkGenericTag(v); ok {
					genericParams = gp
				} else {
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
	if len(genericParams) == 0 {
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

		if w.Define(typeSym) {
			w.Root.Elements = append(w.Root.Elements, tdefNode)
		} else {
			ThrowMultiDefError(typeSym.Name, typeSymPos)
			return false
		}

	}

	// TODO: generic stuff

	return true
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
	case "tupled_suffix":
		// TODO: named tuples -- decide on implementation
	case "struct_suffix":
		members := make(map[string]*types.StructMember)

		for _, item := range branch.Content {
			// subbranch = `struct_member`
			if subbranch, ok := item.(*syntax.ASTBranch); ok {
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
							if dt_, ok := w.walkTypeExt(v); ok {
								dt = dt_
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
			}
		}

		var packed bool
		if _, ok := w.CtxAnnotations["packed"]; ok {
			packed = true
		}

		return types.NewStructType(tdn.Sym.Name, members, packed), true
	}

	return nil, false
}

// walkInterfDef walks an `interf_def` node
func (w *Walker) walkInterfDef(branch *syntax.ASTBranch) bool {
	// TODO: generic stuff

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

	interfBlock := &common.HIRInterfDef{
		Sym: interfSym,
	}

	// extract the methods of the interface
	if w.walkInterfBody(branch.Last().(*syntax.ASTBranch), tInterf.Methods, func(m common.HIRNode) {
		interfBlock.Methods = append(interfBlock.Methods, m)
	}) {
		return false
	}

	// define and load up everything
	if w.Define(interfSym) {
		return true
	}

	ThrowMultiDefError(interfSym.Name, branch.Content[1].Position())
	return false
}

// walkInterfBind walks an `interf_bind` node
func (w *Walker) walkInterfBind(branch *syntax.ASTBranch) bool {
	return false
}

// extractMethods walks an `interf_body` node and adds the methods it finds to
// the method map it is passed.  DOES NOT PARSE THEIR BODIES.
func (w *Walker) walkInterfBody(branch *syntax.ASTBranch, methods map[string]*types.Method, addMethod func(common.HIRNode)) bool {
	return false
}

// walkFuncDef walks a `func_def` node and extracts a HIRFuncDef from it.
// It does NOT declare the function's symbol nor does it add the HIRFuncDef to
// anything (allows this function to be used a number of places).  It may also
// produce a generic as necessary.
func (w *Walker) walkFuncDef(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	// TODO: generic stuff

	_, boxable := w.CtxAnnotations["intrinsic"]
	ft := &types.FuncType{Boxable: boxable}
	var name string
	var funcBody common.HIRNode
	var argData map[string]*common.HIRArgData

	for _, item := range branch.Content {
		switch v := item.(type) {
		case *syntax.ASTBranch:
			switch v.Name {
			case "signature":
				if amap, ok := w.walkFuncSignature(v, ft, false); ok {
					argData = amap
				} else {
					return nil, true
				}
			case "func_body":
				if l, ok := v.Content[0].(*syntax.ASTLeaf); ok && l.Kind == syntax.CONST {
					ft.Constant = true
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

	return fnode, false
}

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
			// TODO: generic stuff
			break
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

	if w.addOperOverload(opKind, ft) {
		w.Root.Elements = append(w.Root.Elements, &common.HIROperDecl{
			OperKind:    opKind,
			Signature:   ft,
			Annotations: w.CtxAnnotations,
			Body:        funcBody,
			ArgData:     argData,
		})

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
