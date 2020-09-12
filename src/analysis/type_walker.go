package analysis

import (
	"fmt"
	"strconv"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/types"
	"github.com/ComedicChimera/whirlwind/src/util"
)

// walkTypeExt walks a `type_ext` node and returns the type it denotes
func (w *Walker) walkTypeExt(branch *syntax.ASTBranch) (types.DataType, bool) {
	return w.walkTypeLabel(branch.BranchAt(1))
}

// walkTypeList walks a `type_list` node
func (w *Walker) walkTypeList(branch *syntax.ASTBranch) ([]types.DataType, bool) {
	typeList := make([]types.DataType, branch.Len())

	for i, item := range branch.Content {
		if i%2 == 0 {
			if dt, ok := w.walkTypeLabel(item.(*syntax.ASTBranch)); ok {
				typeList[i/2] = dt
			} else {
				return nil, false
			}
		}
	}

	return typeList, true
}

// walkTypeLabel converts a type label (`type` used as type label) to a DataType
func (w *Walker) walkTypeLabel(branch *syntax.ASTBranch) (types.DataType, bool) {
	return w.walkTypeBranch(branch.BranchAt(0), false)
}

// walkTypeBranch walks any branch that exists at the top level of a `type`
// branch (not just those used in the context of type labels -- supporting type
// parameters) and converts it to a data type
func (w *Walker) walkTypeBranch(branch *syntax.ASTBranch, allowHigherKindedTypes bool) (types.DataType, bool) {
	switch branch.Name {
	case "value_type":
		return w.walkValueType(branch)
	case "named_type":
		return w.walkNamedType(branch, allowHigherKindedTypes)
	case "ref_type":
		rt := &types.ReferenceType{}

		for _, item := range branch.Content {
			switch v := item.(type) {
			case *syntax.ASTBranch:
				if dt, ok := w.walkTypeBranch(v, false); ok {
					if _, ok := isReference(dt); ok {
						util.ThrowError(
							"Unable to have a double reference type",
							"Type",
							item.Position(),
						)

						return nil, false
					}

					rt.ElemType = dt
				} else {
					return nil, false
				}
			case *syntax.ASTLeaf:
				switch v.Kind {
				case syntax.OWN:
					rt.Owned = true
				case syntax.LOCAL:
					rt.Local = true // defaults to false for nonlocal
				case syntax.CONST:
					rt.Constant = true
				}
			}
		}

		return rt, true
	}

	return nil, false
}

// walkValueType walks a `value_type` node and produces a data type
func (w *Walker) walkValueType(branch *syntax.ASTBranch) (types.DataType, bool) {
	tbranch := branch.BranchAt(0)

	switch tbranch.Name {
	case "prim_types":
		return types.NewPrimitiveTypeFromLabel(branch.LeafAt(0).Value), true
	case "vec_type":
		if dt, ok := w.walkTypeLabel(tbranch.BranchAt(3)); ok {
			dtOk := false
			switch pt := dt.(type) {
			case *types.PrimitiveType:
				// TODO: should `string` be represented using a different type?
				dtOk = int(*pt) != types.PrimString
			}

			if !dtOk {
				util.ThrowError(
					fmt.Sprintf("Type `%s` is not valid as vector element type", dt.Repr()),
					"Type",
					tbranch.Content[1].Position(),
				)

				return nil, false
			}

			vsize, err := strconv.Atoi(tbranch.LeafAt(1).Value)
			if err != nil {
				util.ThrowError(
					"Unable to interpret integer literal",
					"Usage",
					tbranch.Content[1].Position(),
				)

				return nil, false
			}

			return &types.VectorType{
				ElemType: dt,
				Size:     uint(vsize),
			}, true
		}
	case "col_type":
		makeBuiltinGeneric := func(bt types.DataType, typeList ...types.DataType) (types.DataType, bool) {
			// TODO: open type builtins (for possible late definition)

			if gt, ok := bt.(*types.GenericType); ok {
				if generate, ok := gt.CreateGenerate(typeList); ok {
					return generate, true
				}

				util.ThrowError(
					fmt.Sprintf("Failed to create desired built-in generate of `%s`", bt.Repr()),
					"Usage",
					branch.Position(),
				)
			} else {
				util.ThrowError(
					fmt.Sprintf("Expected builtin type to be a generic not `%s`", bt.Repr()),
					"Usage",
					branch.Position(),
				)
			}

			return nil, false
		}

		// if `col_type` ends with `]`
		if _, ok := branch.Last().(*syntax.ASTLeaf); ok {
			// dictionary
			if branch.Len() == 5 {
				if keyType, ok := w.walkTypeLabel(branch.BranchAt(1)); ok {
					if valueType, ok := w.walkTypeLabel(branch.BranchAt(3)); ok {
						if dictDt, ok := w.getBuiltin("__stddict", branch.Position()); ok {
							return makeBuiltinGeneric(dictDt, keyType, valueType)
						}
					}
				}
			} else /* list */ if elemType, ok := w.walkTypeLabel(branch.BranchAt(1)); ok {
				if listDt, ok := w.getBuiltin("__stdlist", branch.Position()); ok {
					return makeBuiltinGeneric(listDt, elemType)
				}
			}
		} else if elemType, ok := w.walkTypeLabel(branch.BranchAt(2)); ok {
			if arrDt, ok := w.getBuiltin("__stdarray", branch.Position()); ok {
				return makeBuiltinGeneric(arrDt, elemType)
			}

		}

		return nil, false
	case "func_type":
		f := &types.FuncType{Boxed: true, Boxable: true, ReturnType: nothingType}
		for _, item := range tbranch.Content {
			switch v := item.(type) {
			case *syntax.ASTBranch:
				if v.Name == "func_type_args" {
					for _, arg := range v.Content {
						if argBranch, ok := arg.(*syntax.ASTBranch); ok {
							fparam := &types.FuncParam{}
							if argBranch.Name == "func_type_arg" {
								if adt, ok := w.walkTypeLabel(argBranch.BranchAt(argBranch.Len() - 1)); ok {
									fparam.Type = adt

									if argBranch.Len() == 2 {
										fparam.Optional = true
									}
								} else {
									return nil, false
								}
							} else /* func_type_var_arg */ if adt, ok := w.walkTypeLabel(argBranch.BranchAt(1)); ok {
								fparam.Variadic = true
								fparam.Type = adt
							} else {
								return nil, false
							}

							f.Params = append(f.Params, fparam)
						}
					}
				} else /* type_list */ {
					if tl, ok := w.walkTypeList(v); ok {
						if len(tl) == 1 {
							f.ReturnType = tl[0]
						} else {
							f.ReturnType = types.TupleType(tl)
						}
					} else {
						return nil, false
					}
				}
			case *syntax.ASTLeaf:
				if v.Kind == syntax.ASYNC {
					f.Async = true
				}
			}
		}

		return f, true
	case "tup_type":
		tupTypes := make([]types.DataType, (tbranch.Len()-2)/2+1)
		n := 0
		for _, item := range tbranch.Content {
			// only branches here are `type`
			if tlbranch, ok := item.(*syntax.ASTBranch); ok {
				if dt, ok := w.walkTypeLabel(tlbranch); ok {
					tupTypes[n] = dt
					n++
				} else {
					return nil, false
				}
			}
		}

		return types.TupleType(tupTypes), true
	}

	return nil, false
}

// walkNamedType walks a `named_type` node and produces a data type
func (w *Walker) walkNamedType(branch *syntax.ASTBranch, allowHigherKindedTypes bool) (types.DataType, bool) {
	// TODO: free types/resolving types/type parameters

	var genericTypeSpec []types.DataType
	var pnames []PositionedName

	for i := branch.Len() - 1; i >= 0; i-- {
		switch v := branch.Content[i].(type) {
		case *syntax.ASTLeaf:
			if v.Kind == syntax.IDENTIFIER {
				// I love having to type `PositionedName` TWO TIMES just to prepend...
				pnames = append([]PositionedName{
					PositionedName{Name: v.Value, Pos: v.Position()},
				}, pnames...)
			}
		case *syntax.ASTBranch:
			// v.Name always == "type_list"
			if typeList, ok := w.walkTypeList(v); ok {
				genericTypeSpec = typeList
			} else {
				return nil, false
			}
		}
	}

	var dsym *common.Symbol
	if len(pnames) == 1 {
		pname := pnames[0]
		dsym = w.Lookup(pname.Name)

		if dsym == nil {
			ThrowUndefinedError(dsym.Name, pname.Pos)
			return nil, false
		}
	} else {
		sym, err := w.GetSymbolFromPackage(pnames)

		if err != nil {
			util.LogMod.LogError(err)

			return nil, false
		}

		dsym = sym
	}

	if dsym.DefKind != common.SKindTypeDef {
		ThrowSymbolUsageError(dsym.Name, "type definition", pnames[len(pnames)-1].Pos)
		return nil, false
	}

	if len(genericTypeSpec) > 0 {
		if gt, ok := dsym.Type.(*types.GenericType); ok {
			if generate, ok := gt.CreateGenerate(genericTypeSpec); ok {
				return generate, true
			}

			util.ThrowError(
				fmt.Sprintf("Invalid type parameters for the generic type `%s`", gt.Repr()),
				"Type",
				branch.Last().Position(),
			)
		}

		util.ThrowError(
			"Type parameters can only be passed to a generic type",
			"Type",
			branch.Last().Position(),
		)
	} else if !allowHigherKindedTypes {
		// prevent generics from being used as a form of this open type in
		// the label (also check to make sure the current type state isn't
		// generic which would obviously be a problem :D).
		if ot, ok := dsym.Type.(*types.OpenType); ok {
			if ot.BlockGenerics() {
				// if we can BlockGenerics successfully, then we can return safely
				return dsym.Type, true
			}
		} else if _, ok := dsym.Type.(*types.GenericType); !ok {
			// if we do not have a generic, we can return safely
			return dsym.Type, true
		}

		// otherwise, ERROR!
		util.ThrowError(
			"Type label cannot be a generic type",
			"Usage",
			branch.Position(),
		)

		return nil, false
	}

	return dsym.Type, true
}
