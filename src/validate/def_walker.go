package validate

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/logging"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/typing"
)

var validTypeSetIntrinsics = map[string]struct{}{
	"Vector":         {},
	"TypedVector":    {},
	"IntegralVector": {},
	"Tuple":          {},
}

// WalkDef walks the AST of any given definition and attempts to determine if it
// can be defined.  If it can, it returns the produced HIR node and the value
// `true`. If it cannot, then it determines first if the only errors are due to
// missing symbols, in which case it returns a map of unknowns and `true`.
// Otherwise, it returns `false`. All unmentioned values for each case will be
// nil. This is the main definition analysis function and accepts a `definition`
// node.  This function is mainly intended to work with the Resolver and
// PackageAssembler.  It also always returns the name of the definition is
// possible.
func (w *Walker) WalkDef(dast *syntax.ASTBranch) (common.HIRNode, string, map[string]*common.UnknownSymbol, bool) {
	def, dt, ok := w.walkDefRaw(dast)

	// make sure w.currentDefName is always cleared (for contexts where there is
	// no name to override it -- eg. interface binding)
	defer (func() {
		w.currentDefName = ""

		// clear self-type data last so that generics can still recognize it
		w.selfType = nil
		w.selfTypeUsed = false
		w.selfTypeRequiresRef = false
	})()

	if ok {
		// apply generic context before returning definition; we can assume that
		// if we reached this point, there are no unknowns to account for
		def, dt = w.applyGenericContext(def, dt)

		// update the shared opaque symbol with the contents of this definition
		// as necessary (not always resolving since we could be dealing with
		// local functions).  Generic and non-generic should always match up
		// because the opaque type is generated based on the definition
		if w.resolving && w.sharedOpaqueSymbol.SrcPackageID == w.SrcPackage.PackageID && w.sharedOpaqueSymbol.Name == w.currentDefName {
			if ot, ok := w.sharedOpaqueSymbol.Type.(*typing.OpaqueType); ok {
				ot.EvalType = dt
			} else if ogt, ok := w.sharedOpaqueSymbol.Type.(*typing.OpaqueGenericType); ok {
				// none of the errors caused by this should effect the
				// generation of this definition -- don't cause unnecessary
				// errors (even in recursive case error is already logged)
				ogt.Evaluate(dt.(*typing.GenericType), w.solver)
			}
		}

		return def, w.currentDefName, nil, true
	}

	// clear the generic context
	w.genericCtx = nil

	// handle fatal definition errors
	if w.fatalDefError {
		w.fatalDefError = false

		return nil, "", nil, false
	}

	// collect and clear our unknowns after they have collected for the definition
	unknowns := w.unknowns
	w.clearUnknowns()

	return nil, w.currentDefName, unknowns, true
}

// walkDefRaw walks a definition without handling any generics or any of the
// clean up. If this function succeeds, it returns the node generated and the
// internal "type" of the node for use in generics as necessary.  It effectively
// acts a wrapper to all of the individual `walk` functions for the various
// kinds of definitions.
func (w *Walker) walkDefRaw(dast *syntax.ASTBranch) (common.HIRNode, typing.DataType, bool) {
	switch dast.Name {
	case "type_def":
		if tdnode, ok := w.walkTypeDef(dast); ok {
			return tdnode, tdnode.Sym.Type, true
		}
	case "func_def":
		if fnnode, ok := w.walkFuncDef(dast, false); ok {
			return fnnode, fnnode.Sym.Type, true
		}
	case "interf_def":
		if itnode, ok := w.walkInterfDef(dast); ok {
			return itnode, itnode.Sym.Type, true
		}
	case "interf_bind":
		if itbind, ok := w.walkInterfBind(dast); ok {
			// our interf binds are never generic types (in the sense that they
			// never use `w.applyGenericContext` or have a generic ctx) so we
			// can just return nil as our data type.  It will also never
			// correspond to a share opaque symbol => no need for data type
			return itbind, nil, true
		}
	case "special_def":
		if specNode, ok := w.walkTopLevelFuncSpecial(dast); ok {
			// special definitions are also never generic types so
			// `w.applyGenericContext` is never involved
			return specNode, nil, true
		}
	}

	return nil, nil, false
}

// walkTypeDef walks a `type_def` node and returns the appropriate HIRNode and a
// boolean indicating if walking was successful.  It does not indicate if an
// errors were fatal.  This should be checked using the `FatalDefError` flag.
func (w *Walker) walkTypeDef(dast *syntax.ASTBranch) (*common.HIRTypeDef, bool) {
	closedType := false
	var name string
	var namePosition *logging.TextPosition
	var dt typing.DataType
	fieldInits := make(map[string]common.HIRNode)

	for _, item := range dast.Content {
		switch v := item.(type) {
		case *syntax.ASTBranch:
			switch v.Name {
			case "generic_tag":
				if !w.primeGenericContext(v, false) {
					return nil, false
				}
			case "newtype":
				if w.hasFlag("intrinsic") {
					w.logInvalidIntrinsic(name, "defined type", namePosition)
					return nil, false
				}

				// indents and dedents are removed by parser so suffix will
				// always be first element
				suffixNode := v.BranchAt(0)
				if suffixNode.Name == "alg_suffix" {
					if adt, ok := w.walkAlgebraicSuffix(suffixNode, name, namePosition); ok {
						dt = adt
					} else {
						return nil, false
					}
				} else {
					if sdt, ok := w.walkStructSuffix(suffixNode, name, fieldInits); ok {
						dt = sdt
					} else {
						return nil, false
					}
				}

			case "typeset":
				// NOTE: typesets don't define a self-type since such a
				// definition would have literally no meaning (especially if it
				// only contained one type -- itself)
				if types, ok := w.walkOffsetTypeList(v, 1, 0); ok {
					intrinsic := w.hasFlag("intrinsic")

					if intrinsic {
						if _, ok := validTypeSetIntrinsics[name]; !ok {
							w.logInvalidIntrinsic(name, "type set", namePosition)
							return nil, false
						}
					}

					dt = &typing.TypeSet{
						Name:         name,
						SrcPackageID: w.SrcPackage.PackageID,
						Types:        types,
						Intrinsic:    intrinsic,
					}
				} else {
					return nil, false
				}
			}
		case *syntax.ASTLeaf:
			switch v.Kind {
			case syntax.CLOSED:
				closedType = true
			case syntax.IDENTIFIER:
				name = v.Value
				w.currentDefName = name
				namePosition = v.Position()
			}
		}
	}

	symbol := &common.Symbol{
		Name:       name,
		Type:       dt,
		DefKind:    common.DefKindTypeDef,
		DeclStatus: w.DeclStatus,
		Constant:   true,
	}

	if !w.define(symbol) {
		w.logRepeatDef(name, namePosition, true)
		return nil, false
	}

	// only declare algebraic variants if the definition of the core algebraic
	// type succeeded (that way we don't have random definitions floating around
	// without a parent)
	if at, ok := dt.(*typing.AlgebraicType); ok {
		at.Closed = closedType

		if !closedType {
			for _, vari := range at.Variants {
				symbol := &common.Symbol{
					Name:       vari.Name,
					Type:       vari,
					Constant:   true,
					DefKind:    common.DefKindTypeDef,
					DeclStatus: w.DeclStatus,
				}

				if !w.define(symbol) {
					w.logFatalDefError(
						fmt.Sprintf("Algebraic type `%s` must be marked `closed` as its variant `%s` shares a name with an already-defined symbol", name, vari.Name),
						logging.LMKName,
						namePosition,
					)
					return nil, false
				}
			}
		}
	} else if closedType {
		// you can't use `closed` on a type that isn't algebraic
		w.logFatalDefError(
			fmt.Sprintf("`closed` property not applicable on type `%s`", dt.Repr()),
			logging.LMKDef,
			dast.Content[0].Position(),
		)
		return nil, false
	}

	return &common.HIRTypeDef{
		Sym:        symbol,
		FieldInits: fieldInits,
	}, true
}

// walkAlgebraicSuffix walks an `alg_suffix` node in a type definition
func (w *Walker) walkAlgebraicSuffix(suffix *syntax.ASTBranch, name string, namePosition *logging.TextPosition) (typing.DataType, bool) {
	algType := &typing.AlgebraicType{
		Name:         name,
		SrcPackageID: w.SrcPackage.PackageID,
	}

	// set the selfType field
	w.setSelfType(algType)

	for _, item := range suffix.Content {
		algVariBranch := item.(*syntax.ASTBranch)

		algVariant := &typing.AlgebraicVariant{
			Parent: algType,
			Name:   algVariBranch.LeafAt(1).Value,
		}

		if algVariBranch.Len() == 3 {
			if values, ok := w.walkOffsetTypeList(algVariBranch.BranchAt(2), 1, 1); ok {
				algVariant.Values = values
			} else {
				return nil, false
			}
		}

		algType.Variants = append(algType.Variants, algVariant)
	}

	// if the selfType is used in the only variant then the type is recursively
	// defined and has no alternate form that prevents such recursion (ie. no
	// "base case") and so we must throw an error.
	if w.selfTypeUsed && len(algType.Variants) == 1 {
		w.logFatalDefError(
			fmt.Sprintf("Algebraic type `%s` defined recursively with no base case", name),
			logging.LMKDef,
			namePosition,
		)
		return nil, false
	}

	return algType, true
}

// walkStructSuffix walks a `struct_suffix` node in a type definition
func (w *Walker) walkStructSuffix(suffix *syntax.ASTBranch, name string, fieldInits map[string]common.HIRNode) (typing.DataType, bool) {
	structType := &typing.StructType{
		Name:         name,
		SrcPackageID: w.SrcPackage.PackageID,
		Packed:       w.hasFlag("packed"),
		Fields:       make(map[string]*typing.TypeValue),
	}

	// set the selfType field and appropriate flag
	w.setSelfType(structType)
	w.selfTypeRequiresRef = true

	for _, item := range suffix.Content {
		if branch, ok := item.(*syntax.ASTBranch); ok {
			if branch.Name == "struct_member" {
				if fnames, tv, init, ok := w.walkTypeValues(branch, "fields"); ok {
					// multiple fields can share the same type value and
					// initializer (for efficiency)
					for fname := range fnames {
						// duplicates names already checked in `w.walkTypeValues`
						structType.Fields[fname] = tv

						if init != nil {
							fieldInits[fname] = init
						}
					}
				} else {
					return nil, false
				}
			} else /* inherit */ {
				if dt, ok := w.walkTypeLabel(branch); ok {
					if st, ok := dt.(*typing.StructType); ok {
						// inherits cannot be self-referential
						if typing.Equals(st, structType) {
							w.logFatalDefError(
								fmt.Sprintf("Struct `%s` cannot inherit from itself", name),
								logging.LMKDef,
								branch.Position(),
							)
							return nil, false
						}

						structType.Inherit = st
					} else {
						// structs can only inherit from other structs
						w.logFatalDefError(
							fmt.Sprintf("Struct `%s` must inherit from another struct not `%s`", name, dt.Repr()),
							logging.LMKDef,
							branch.Position(),
						)
						return nil, false
					}
				} else {
					return nil, false
				}
			}
		}
	}

	return structType, true
}

// walkTypeValues walks any node that is of the form of a type value (ie.
// `identifier_list` followed by `type_ext`) and generates a single common type
// value and a map of names and positions from it.  It also handles `vol` and
// `const` modifiers and returns any initializers it finds.  The `nameKind`
// parameter is the type of thing that the names in the type value are (eg.
// `field` or `argument`) -- this function does check for duplicate identifiers.
func (w *Walker) walkTypeValues(branch *syntax.ASTBranch, nameKind string) (map[string]*logging.TextPosition, *typing.TypeValue, common.HIRNode, bool) {
	ctv := &typing.TypeValue{}
	var names map[string]*logging.TextPosition
	var initializer common.HIRNode

	for _, item := range branch.Content {
		switch v := item.(type) {
		case *syntax.ASTBranch:
			switch v.Name {
			case "identifier_list":
				if _names, ok := w.walkIdList(v, nameKind); ok {
					names = _names
				} else {
					return nil, nil, nil, false
				}
			case "type_ext":
				if dt, ok := w.walkTypeExt(v); ok {
					ctv.Type = dt
				} else {
					return nil, nil, nil, false
				}
			case "initializer":
				initializer = (*common.HIRIncomplete)(v.BranchAt(1))
			}
		case *syntax.ASTLeaf:
			switch v.Kind {
			case syntax.VOL:
				ctv.Volatile = true
			case syntax.CONST:
				ctv.Constant = true
			}
		}
	}

	return names, ctv, initializer, true
}

// walkFuncDef walks a function or method definition
func (w *Walker) walkFuncDef(branch *syntax.ASTBranch, isMethod bool) (*common.HIRFuncDef, bool) {
	var name string
	var namePosition *logging.TextPosition
	funcType := &typing.FuncType{Boxable: !w.hasFlag("intrinsic")}
	var initializers map[string]common.HIRNode
	var body common.HIRNode

	for _, item := range branch.Content {
		switch v := item.(type) {
		case *syntax.ASTBranch:
			switch v.Name {
			case "generic_tag":
				if !w.primeGenericContext(v, false) {
					return nil, false
				}
			case "signature":
				if args, adata, rtType, ok := w.walkSignature(v); ok {
					funcType.Args = args
					initializers = adata
					funcType.ReturnType = rtType
				} else {
					return nil, false
				}
			case "decl_func_body":
				body = w.extractFuncBody(v)
			}
		case *syntax.ASTLeaf:
			switch v.Kind {
			case syntax.ASYNC:
				funcType.Async = true
			case syntax.IDENTIFIER:
				name = v.Value
				w.currentDefName = name
				namePosition = v.Position()
			}
		}
	}

	if !isMethod && body == nil {
		if !(w.hasFlag("intrinsic") ||
			w.hasFlag("external") ||
			w.hasFlag("dllimport")) {

			w.logFatalDefError(
				fmt.Sprintf("Function `%s` must have a body", name),
				logging.LMKDef,
				namePosition,
			)

			return nil, false
		}
	}

	sym := &common.Symbol{
		Name:       name,
		Type:       funcType,
		DeclStatus: w.DeclStatus,
		DefKind:    common.DefKindFuncDef,
		Constant:   true,
	}

	if !isMethod && !w.define(sym) {
		w.logRepeatDef(name, namePosition, true)
		return nil, false
	}

	return &common.HIRFuncDef{
		Sym:          sym,
		Annotations:  w.annotations,
		Initializers: initializers,
		Body:         body,
	}, true
}

// walkSignature walks a `signature` node (used for functions, operator
// definitions, etc.)
func (w *Walker) walkSignature(branch *syntax.ASTBranch) ([]*typing.FuncArg,
	map[string]common.HIRNode, typing.DataType, bool) {

	var args []*typing.FuncArg
	initializers := make(map[string]common.HIRNode)

	argsDecl := branch.BranchAt(0)
	if argsDecl.Len() > 2 {
		if !w.walkRecursiveRepeat(argsDecl.Content[1:argsDecl.Len()-1], func(argBranch *syntax.ASTBranch) bool {
			if argBranch.Name == "var_arg_decl" {
				name := argBranch.LeafAt(1).Value
				if _, ok := initializers[name]; ok {
					w.logFatalDefError(
						fmt.Sprintf("Multiple arguments named `%s`", name),
						logging.LMKName,
						argBranch.Content[1].Position(),
					)

					return false
				} else {
					initializers[name] = nil
				}

				if rt, ok := w.walkTypeExt(argBranch.BranchAt(2)); ok {
					args = append(args, &typing.FuncArg{
						Name: name,
						Val: &typing.TypeValue{
							Type: rt,
						},
						Indefinite: true,
					})

					return true
				}
			} else {
				// argument duplication checked in `walkTypeValues`
				if argNames, tv, initializer, ok := w.walkTypeValues(argBranch, "arguments"); ok {
					if initializer != nil {
						for name := range argNames {
							args = append(args, &typing.FuncArg{
								Name:     name,
								Optional: true,
								Val:      tv,
							})

							initializers[name] = initializer
						}
					} else {
						for name := range argNames {
							args = append(args, &typing.FuncArg{
								Name: name,
								Val:  tv,
							})
						}
					}

					return true
				}
			}

			return false
		}) {
			return nil, nil, nil, false
		}
	}

	if branch.Len() == 2 {
		if rtType, ok := w.walkTypeLabel(branch.BranchAt(1)); ok {
			return args, initializers, rtType, true
		} else {
			return nil, nil, nil, false
		}
	}

	return args, initializers, nil, false
}

// extractFuncBody extracts the evaluable node (`do_block`, `expr`) of any kind
// of function body if one exists (inc. closure bodies, etc)
func (w *Walker) extractFuncBody(branch *syntax.ASTBranch) common.HIRNode {
	for _, item := range branch.Content {
		if abranch, ok := item.(*syntax.ASTBranch); ok {
			// abranch is always either `expr` or `do_block` => evaluable node
			return (*common.HIRIncomplete)(abranch)
		}
	}

	// no evaluable node (definition with no body)
	return nil
}

// walkInterfDef walks a conceptual interface definition
func (w *Walker) walkInterfDef(branch *syntax.ASTBranch) (*common.HIRInterfDef, bool) {
	it := &typing.InterfType{
		Name:         branch.LeafAt(1).Value,
		SrcPackageID: w.Context.PackageID,
		Methods:      make(map[string]*typing.InterfMethod),
	}

	w.currentDefName = it.Name
	w.setSelfType(it)

	// only way the third item is an AST branch is if it is a generic tag
	if genericTag, ok := branch.Content[2].(*syntax.ASTBranch); ok {
		if !w.primeGenericContext(genericTag, true) {
			return nil, false
		}
	}

	methodNodes, ok := w.walkInterfBody(branch.Last().(*syntax.ASTBranch), it, true)
	if !ok {
		return nil, false
	}

	// interfaces are, for the purposes of definition checking, type definitions
	sym := &common.Symbol{
		Name:       it.Name,
		Type:       it,
		DefKind:    common.DefKindTypeDef,
		DeclStatus: w.DeclStatus,
		Constant:   true,
	}

	if !w.define(sym) {
		w.logRepeatDef(sym.Name, branch.Content[1].Position(), true)
		return nil, false
	}

	// move the interface generic context into the regular generic context so
	// that generic interfaces can be properly handled
	w.genericCtx = w.interfGenericCtx
	w.interfGenericCtx = nil

	return &common.HIRInterfDef{
		Sym:     sym,
		Methods: methodNodes,
	}, true
}

// walkInterfBody is used to walk the bodies of both kinds of interfaces (the `interf_body` node)
func (w *Walker) walkInterfBody(body *syntax.ASTBranch, it *typing.InterfType, conceptual bool) ([]common.HIRNode, bool) {
	var methodNodes []common.HIRNode

	for _, item := range body.Content {
		// only node is `interfMember` (methods)
		if interfMember, ok := item.(*syntax.ASTBranch); ok {
			var name string
			var namePosition *logging.TextPosition
			var node common.HIRNode
			var dt typing.DataType
			var methodKind int

			methodBranch := interfMember.BranchAt(0)
			switch methodBranch.Name {
			case "func_def":
				if fnnode, ok := w.walkFuncDef(methodBranch, true); ok {
					name = fnnode.Sym.Name
					dt = fnnode.Sym.Type
					node = fnnode

					// the name of a function is always the second node
					namePosition = methodBranch.Content[1].Position()

					if conceptual {
						if fnnode.Body == nil {
							methodKind = typing.MKAbstract
						} else {
							methodKind = typing.MKVirtual
						}
					} else if fnnode.Body == nil {
						w.logFatalDefError(
							"Type interface may not contain abstract methods",
							logging.LMKInterf,
							namePosition,
						)

						return nil, false
					}

					methodKind = typing.MKStandard
				}
			case "special_def":
				if specNode, ok := w.walkFuncSpecial(methodBranch, func(name string, pos *logging.TextPosition) (typing.DataType, bool) {
					if method, ok := it.Methods[name]; ok {
						if method.Kind == typing.MKAbstract {
							w.logFatalDefError(
								"Unable to define specialization for abstract method",
								logging.LMKGeneric,
								pos,
							)
							return nil, false
						}

						return method.Signature, true
					}

					w.LogUndefined(name, pos)
					return nil, false
				}); ok {
					methodNodes = append(methodNodes, specNode)
					continue
				}
				// TODO: other method types (specializations, etc.)
			}

			// apply the generic context of any methods should such context
			// exist (basically a reimplementation of `applyGenericContext` for
			// methods of an interface; working without a symbol)
			if w.genericCtx != nil {
				var gt *typing.GenericType
				if w.selfType != nil {
					gt = w.selfType.(*typing.GenericType)
				} else {
					gt = &typing.GenericType{
						TypeParams: w.genericCtx,
						Template:   dt,
					}
				}

				dt = gt

				node = &common.HIRGeneric{
					Generic:     gt,
					GenericNode: node,
				}

				w.genericCtx = nil
			}

			// methods cannot be duplicated (just create a name error)
			if _, ok := it.Methods[name]; ok {
				w.logRepeatDef(name, namePosition, true)
				return nil, false
			}

			it.Methods[name] = &typing.InterfMethod{
				Signature: dt,
				Kind:      methodKind,
			}

			methodNodes = append(methodNodes, node)
		}
	}

	return methodNodes, true
}

// walkInterfBind walks a normal or generic interface binding
func (w *Walker) walkInterfBind(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	var it *typing.InterfType
	var bindDt typing.DataType
	var methodNodes []common.HIRNode

	implInterfs := make(map[*typing.InterfType]*logging.TextPosition)

	for _, item := range branch.Content {
		if itembranch, ok := item.(*syntax.ASTBranch); ok {
			switch itembranch.Name {
			case "generic_tag":
				if w.primeGenericContext(itembranch, true) {
					// set the wildcard types to immediate bind (for generic
					// bindings -- during matching)
					for _, wc := range w.interfGenericCtx {
						wc.ImmediateBind = true
					}
				} else {
					return nil, false
				}
			case "type":
				if dt, ok := w.walkTypeLabel(itembranch); ok {
					// once bindDt is set, all other types must be types to
					// derive => add them to implInterfs
					if bindDt == nil {
						bindDt = dt
					} else if implIt, ok := typing.InnerType(dt).(*typing.InterfType); ok {
						implInterfs[implIt] = itembranch.Position()
					} else {
						w.logFatalDefError(
							fmt.Sprintf("Binding may only derive interfaces not `%s`", dt.Repr()),
							logging.LMKInterf,
							itembranch.Position(),
						)

						return nil, false
					}
				}
			case "interf_body":
				if mnodes, ok := w.walkInterfBody(itembranch, it, false); ok {
					methodNodes = mnodes
				} else {
					return nil, false
				}
			}
		}
	}

	// typeInterf is the final data type created (once generics are applied)
	var typeInterf typing.DataType

	// node is our final HIRNode (`HIRInterfBind` or `HIRGenericBind`)
	var node common.HIRNode

	// create a generic interface for our type interface if necessary
	if w.interfGenericCtx != nil {
		// there is no prebuilt generic in our self-type so we can just create a
		// new generic for the type interface; however, we need to create a copy
		// of the wildcard types that are not set to immediate bind (otherwise,
		// our generics will not behave as expected)
		typeParams := make([]*typing.WildcardType, len(w.interfGenericCtx))
		for i, wc := range w.interfGenericCtx {
			typeParams[i] = &typing.WildcardType{
				Name:          wc.Name,
				Restrictors:   wc.Restrictors,
				ImmediateBind: false,
				// we can ignore the `Value` field here
			}
		}

		typeInterf = &typing.GenericType{
			Template:   it,
			TypeParams: typeParams,
		}
	} else {
		typeInterf = it
	}

	// create the HIRNode (same for generic and non-generic types)
	node = &common.HIRInterfBind{
		Interf: &common.Symbol{
			Type:       typeInterf,
			DefKind:    common.DefKindBinding,
			DeclStatus: w.DeclStatus,
			Constant:   true,
		},
		BoundType: bindDt,
		Methods:   methodNodes,
	}

	// create and add the binding
	binding := &typing.Binding{
		MatchType:  bindDt,
		Wildcards:  w.interfGenericCtx,
		TypeInterf: typeInterf,
		Exported:   w.DeclStatus == common.DSExported,
	}

	w.SrcPackage.GlobalBindings.Bindings = append(w.SrcPackage.GlobalBindings.Bindings, binding)

	// clear our interface generic context
	w.interfGenericCtx = nil

	// check and apply any of our explicit implementations
	for implInterf, pos := range implInterfs {
		if w.solver.ImplementsInterf(bindDt, implInterf) {
			w.solver.Derive(it, implInterf)
		} else {
			w.logFatalDefError(
				fmt.Sprintf("Type interface for `%s` does not fully implement interface `%s`", bindDt.Repr(), implInterf.Repr()),
				logging.LMKInterf,
				pos,
			)
		}
	}

	return node, true
}

// walkTopLevelFuncSpecial walks a function specialization not contained within
// an interface -- that is one at the top level of the program
func (w *Walker) walkTopLevelFuncSpecial(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	return w.walkFuncSpecial(branch, func(name string, pos *logging.TextPosition) (typing.DataType, bool) {
		sym, fpkg, ok := w.Lookup(name)

		if ok {
			if sym.DefKind != common.DefKindFuncDef {
				w.logFatalDefError(
					"Function specialization may only be applied to generic functions",
					logging.LMKGeneric,
					pos,
				)

				return nil, false
			}
		} else {
			if w.resolving {
				if w.sharedOpaqueSymbol.SrcPackageID == w.SrcPackage.PackageID && w.sharedOpaqueSymbol.Name == name {
					// shared opaque symbol can only share things that aren't functions => specialization is invalid
					w.logFatalDefError(
						"Function specialization may only be applied to generic functions",
						logging.LMKGeneric,
						pos,
					)
				} else {
					w.unknowns[name] = &common.UnknownSymbol{
						Name:           name,
						Position:       pos,
						ForeignPackage: fpkg,
					}
				}
			} else {
				w.LogUndefined(name, pos)
			}

			return nil, false
		}

		return sym.Type, true
	})
}

// walkFuncSpecial walks a function specialization and returns the generated
// node and *typing.GenericSpecialization.  This function can be used for
// function specializations of both methods and functions.  To this end, it
// takes a lookup function as an argument that should return the first data type
// that matches the given name and a flag boolena
func (w *Walker) walkFuncSpecial(branch *syntax.ASTBranch,
	lookup func(string, *logging.TextPosition) (typing.DataType, bool)) (common.HIRNode, bool) {

	var gt *typing.GenericType
	genericSpecial := &typing.GenericSpecialization{}
	var typeListBranch *syntax.ASTBranch
	var body common.HIRNode

	for _, item := range branch.Content {
		switch v := item.(type) {
		case *syntax.ASTBranch:
			switch v.Name {
			case "generic_tag":
				// we can just use generic context is a place to store our
				// parametric specialization parameters and indicate whether or
				// not this specialization is parametric
				if !w.primeGenericContext(v, false) {
					return nil, false
				}
			case "type_list":
				if typeList, ok := w.walkTypeList(v); ok {
					typeListBranch = v
					genericSpecial.MatchingTypes = typeList
				} else {
					return nil, false
				}
			case "special_func_body":
				body = (*common.HIRIncomplete)(v)
			}
		case *syntax.ASTLeaf:
			if v.Kind == syntax.IDENTIFIER {
				if dt, ok := lookup(v.Value, v.Position()); ok {
					// only types that are valid here are generic types (no
					// opaque generic types)
					if gt, ok = dt.(*typing.GenericType); ok {
						// just use the builtin create generic instance method
						// to check the specialization parameters. We can
						// actually just leave the instance as pregenerated
						// since it will be skipped during generic evaluation
						// (since a specialization exists) and since we know
						// there is a valid specialization, we know all
						// instances matching the specialization are valid as
						// well
						if _, ok = w.solver.CreateGenericInstance(gt, genericSpecial.MatchingTypes, typeListBranch); !ok {
							w.fatalDefError = true
							return nil, false
						}

						gt.Specializations = append(gt.Specializations, genericSpecial)
					} else {
						w.logFatalDefError(
							"Function specialization is only valid on generic functions",
							logging.LMKGeneric,
							v.Position(),
						)
					}
				} else {
					return nil, false
				}
			}
		}
	}

	if w.genericCtx == nil {
		return &common.HIRSpecialDef{
			RootGeneric: gt,
			TypeParams:  genericSpecial.MatchingTypes,
			Body:        body,
		}, true
	} else {
		// clear the generic context since we no longer need it as a flag
		w.genericCtx = nil

		// set up the shared slice that both the typing.GenericSpecialization
		// and the common.HIRParametricSpecialDef will share.  It can just
		// be `nil` for now.
		var parametricInstanceSlice [][]typing.DataType

		genericSpecial.ParametricInstances = &parametricInstanceSlice
		return &common.HIRParametricSpecialDef{
			RootGeneric:         gt,
			TypeParams:          genericSpecial.MatchingTypes,
			ParametricInstances: &parametricInstanceSlice,
			Body:                body,
		}, true
	}
}
