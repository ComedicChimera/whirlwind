package analysis

import (
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
			if w.walkTypeDef(defNode) {
				return true
			}
		case "interf_def":
		case "interf_bind":
		case "operator_def":
		case "func_def":
		case "decorator":
		case "annotated_def":
		case "variable_decl":
		case "variant_def":
		}
	}

	return false
}

// walkTypeDef walks a `type_def` node
func (w *Walker) walkTypeDef(branch *syntax.ASTBranch) bool {
	typeSym := &common.Symbol{}
	tdefNode := &common.HIRTypeDef{Symbol: typeSym}

	closed := false
	genericParams := make(map[string]*types.TypeParam)

	for _, item := range branch.Content {
		switch v := item.(type) {
		case *syntax.ASTBranch:
			switch v.Name {
			case "generic_tag":
				if gp, ok := w.walkGenericTag(v); ok {
					genericParams = gp
				} else {
					return true
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
							return true
						}
					}
				}

				typeSym.Type = ts
			case "newtype":
				if nt, ok := w.walkNewType(v, tdefNode); ok {
					typeSym.Type = nt
				} else {
					return true
				}
			}
		case *syntax.ASTLeaf:
			switch v.Kind {
			case syntax.CLOSED:
				closed = true
			case syntax.IDENTIFIER:
				typeSym.Name = v.Value
			}
		}
	}

	if !closed {

	}

	_ = genericParams

	return false
}

func (w *Walker) walkNewType(branch *syntax.ASTBranch, tdn *common.HIRTypeDef) (types.DataType, bool) {
	switch branch.Name {
	case "enum_suffix":
		ts := types.NewTypeSet(
			tdn.Symbol.Name,
			types.TSKindEnum,
			nil,
		)

		for _, item := range branch.Content {
			mnode := item.(*syntax.ASTBranch)

			if mnode.Len() == 2 {
				ts.NewEnumMember(mnode.LeafAt(1).Value, nil)
				continue
			} else if typeList, ok := w.walkTypeList(mnode.BranchAt(2).BranchAt(1)); ok {
				if ts.NewEnumMember(
					mnode.LeafAt(1).Value,
					typeList,
				) {
					ts.SetKind = types.TSKindAlgebraic
					continue
				}
			}

			return nil, false
		}

		return ts, true
	case "tupled_suffix":
		// TODO: named tuples - decide on implementation
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
									util.LogMod.LogError(util.NewWhirlError(
										"Each struct member must have a unique name",
										"Name",
										subbranch.BranchAt(0).Content[i*2].Position(),
									))

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

		// TODO: annotations :)
		return types.NewStructType(tdn.Symbol.Name, members, packed), true
	}

	return nil, false
}
