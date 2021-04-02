package validate

import (
	"fmt"

	"whirlwind/common"
	"whirlwind/logging"
	"whirlwind/syntax"
	"whirlwind/typing"
)

var validTypeSetIntrinsics = map[string]struct{}{
	"Vector":         {},
	"TypedVector":    {},
	"IntegralVector": {},
	"Tuple":          {},
}

// WalkDef walks the AST of any given definition and attempts to determine if it
// can be defined.  If it can, it returns the produced HIR node and the value
// `true`. Otherwise, it returns `false`. All unmentioned values for each case
// will be nil. This is the main definition analysis function and accepts a
// definition core (that is a node inside a `definition` like `func_def`).  This
// function is mainly intended to work with the Resolver and PackageAssembler.
func (w *Walker) WalkDef(dast *syntax.ASTBranch, declStatus int) (common.HIRNode, bool) {
	w.declStatus = declStatus
	def, dt, ok := w.walkDefRaw(dast)

	// make sure w.currentDefName is always cleared (for contexts where there is
	// no name to override it -- eg. interface binding)
	defer (func() {
		w.currentDefName = ""

		// reset to default decl status
		w.declStatus = common.DSInternal

		// clear self-type data last so that generics can still recognize it
		w.selfType = nil
		w.selfTypeUsed = false
		w.selfTypeRequiresRef = false
	})()

	if ok {
		// apply generic context before returning definition
		def, dt = w.applyGenericContext(def, dt)

		// update the shared opaque symbol with the contents of this definition
		// as necessary (not always resolving since we could be dealing with
		// local functions).  Generic and non-generic should always match up
		// because the opaque type is generated based on the definition
		if w.resolving {
			if os, ok := w.sharedOpaqueSymbolTable.LookupOpaque(w.SrcPackage.PackageID, w.currentDefName); ok {
				if ot, ok := os.Type.(*typing.OpaqueType); ok {
					ot.EvalType = dt
				} else if ogt, ok := os.Type.(*typing.OpaqueGenericType); ok {
					// none of the errors caused by this should effect the
					// generation of this definition -- don't cause unnecessary
					// errors (even in recursive case error is already logged)
					ogt.Evaluate(dt.(*typing.GenericType), w.solver)
				}
			}
		}

		return def, true
	}

	// clear the generic context (not cleared by `applyGenericContext` on this
	// code path)
	w.genericCtx = nil

	return nil, false
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
			return tdnode, tdnode.Type, true
		}
	case "func_def":
		if fnnode, ok := w.walkFuncDef(dast, false); ok {
			return fnnode, fnnode.Type, true
		}
	case "interf_def":
		if itnode, ok := w.walkInterfDef(dast); ok {
			return itnode, itnode.Type, true
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
		if specNode, ok := w.walkFuncSpecial(dast); ok {
			// special definitions are also never generic types so
			// `w.applyGenericContext` is never involved
			return specNode, nil, true
		}
	case "annotated_def":
		return w.walkAnnotatedDef(dast)
	case "operator_def":
		if opdefNode, ok := w.walkOperatorDef(dast); ok {
			// operator definitions have special logic for their generics
			// so `applyGenericContext` will never be used meaning we can
			// freely return a `nil` data type
			return opdefNode, nil, true
		}
	}

	return nil, nil, false
}

// walkTypeDef walks a `type_def` node and returns the appropriate HIRNode and a
// boolean indicating if walking was successful.
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
		DeclStatus: w.declStatus,
		Constant:   true,
	}

	if !w.define(symbol) {
		w.logRepeatDef(name, namePosition, true)
		return nil, false
	}

	tdef := &common.HIRTypeDef{
		Name:       name,
		Type:       dt,
		FieldInits: fieldInits,
	}

	symbol.DefNode = tdef

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
					DeclStatus: w.declStatus,
					DefNode:    tdef,
				}

				if !w.define(symbol) {
					w.logError(
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
		w.logError(
			fmt.Sprintf("`closed` property not applicable on type `%s`", dt.Repr()),
			logging.LMKDef,
			dast.Content[0].Position(),
		)
		return nil, false
	}

	return tdef, true
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
		w.logError(
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
							w.logError(
								fmt.Sprintf("Struct `%s` cannot inherit from itself", name),
								logging.LMKDef,
								branch.Position(),
							)
							return nil, false
						}

						structType.Inherit = st
					} else {
						// structs can only inherit from other structs
						w.logError(
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
				if args, adata, rtType, ok := w.walkSignature(v, false); ok {
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
				namePosition = v.Position()

				// if name == "clamp" {
				// 	fmt.Println("found clamp")
				// }

				// only set current def name if we are not in a method;
				// otherwise, we will override the interface definition
				if !isMethod {
					w.currentDefName = name
				}
			}
		}
	}

	if !isMethod && body == nil {
		if !(w.hasFlag("intrinsic") ||
			w.hasFlag("external") ||
			w.hasFlag("dllimport")) {

			w.logError(
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
		DeclStatus: w.declStatus,
		DefKind:    common.DefKindFuncDef,
		Constant:   true,
	}

	if !isMethod && !w.define(sym) {
		w.logRepeatDef(name, namePosition, true)
		return nil, false
	}

	fdef := &common.HIRFuncDef{
		Name:         name,
		Type:         funcType,
		Annotations:  w.annotations,
		Initializers: initializers,
		Body:         body,
	}

	sym.DefNode = fdef

	return fdef, true
}

// walkSignature walks a `signature` node (used for functions, operator
// definitions, etc.)
func (w *Walker) walkSignature(branch *syntax.ASTBranch, isOperator bool) ([]*typing.FuncArg,
	map[string]common.HIRNode, typing.DataType, bool) {

	var args []*typing.FuncArg
	initializers := make(map[string]common.HIRNode)

	argsDecl := branch.BranchAt(0)
	if argsDecl.Len() > 2 {
		if !w.walkRecursiveRepeat(argsDecl.Content[1:argsDecl.Len()-1], func(argBranch *syntax.ASTBranch) bool {
			if argBranch.Name == "var_arg_decl" {
				if isOperator {
					w.logError(
						fmt.Sprintf("Operators cannot accept indefinite arguments"),
						logging.LMKDef,
						argBranch.Position(),
					)

					return false
				}

				name := argBranch.LeafAt(1).Value
				if _, ok := initializers[name]; ok {
					w.logError(
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
						if isOperator {
							w.logError(
								"Operators cannot accept optional arguments",
								logging.LMKDef,
								argBranch.Position(),
							)

							return false
						}

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
	} else /* branch.Len() == 1 */ {
		// no arg errors and no return value => rt value of `nothing`
		return args, initializers, &typing.PrimitiveType{PrimKind: typing.PrimKindUnit, PrimSpec: 0}, true
	}
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

	// only way the third item is an AST branch is if it is a generic tag
	if genericTag, ok := branch.Content[2].(*syntax.ASTBranch); ok {
		if !w.primeGenericContext(genericTag, true) {
			return nil, false
		}
	}

	w.setSelfType(it)

	methodNodes, ok := w.walkInterfBody(branch.LastBranch(), it, true)
	if !ok {
		return nil, false
	}

	// interfaces are, for the purposes of definition checking, type definitions
	sym := &common.Symbol{
		Name:       it.Name,
		Type:       it,
		DefKind:    common.DefKindTypeDef,
		DeclStatus: w.declStatus,
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
		Name:    it.Name,
		Type:    it,
		Methods: methodNodes,
	}, true
}

// walkInterfBind walks a normal or generic interface binding
func (w *Walker) walkInterfBind(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	it := &typing.InterfType{
		// Bindings have no name
		Methods:      make(map[string]*typing.InterfMethod),
		SrcPackageID: w.SrcPackage.PackageID,
	}
	var bindDt typing.DataType
	var methodNodes []common.HIRNode

	implInterfs := make(map[*typing.InterfType]*logging.TextPosition)

	var bindTypePos *logging.TextPosition
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
					bindTypePos = item.Position()

					// once bindDt is set, all other types must be types to
					// derive => add them to implInterfs
					if bindDt == nil {
						// interfaces cannot be bound onto references or interfaces
						switch typing.InnerType(dt).(type) {
						// don't need to check for generics since
						// `walkTypeLabel` will never let them out (without
						// erroring first)
						case *typing.InterfType, *typing.RefType:
							w.logError(
								fmt.Sprintf("Cannot bind interface onto type `%s`", dt.Repr()),
								logging.LMKInterf,
								itembranch.Position(),
							)

							return nil, false
						}

						bindDt = dt
					} else if implIt, ok := typing.InnerType(dt).(*typing.InterfType); ok {
						implInterfs[implIt] = itembranch.Position()
					} else {
						w.logError(
							fmt.Sprintf("Binding may only derive interfaces not `%s`", dt.Repr()),
							logging.LMKInterf,
							itembranch.Position(),
						)

						return nil, false
					}
				} else {
					return nil, false
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
		Type:      it,
		BoundType: bindDt,
		Methods:   methodNodes,
	}

	// create and add the binding
	binding := &typing.Binding{
		MatchType:  bindDt,
		Wildcards:  w.interfGenericCtx,
		TypeInterf: typeInterf,
		Exported:   w.declStatus == common.DSExported,
	}

	if w.checkBinding(binding, bindTypePos) {
		w.SrcPackage.GlobalBindings.Bindings = append(w.SrcPackage.GlobalBindings.Bindings, binding)
	} else {
		return nil, false
	}

	// clear our interface generic context
	w.interfGenericCtx = nil

	// check and apply any of our explicit implementations
	for implInterf, pos := range implInterfs {
		if w.solver.ImplementsInterf(bindDt, implInterf) {
			w.solver.Derive(it, implInterf)
		} else {
			w.logError(
				fmt.Sprintf("Type interface for `%s` does not fully implement interface `%s`", bindDt.Repr(), implInterf.Repr()),
				logging.LMKInterf,
				pos,
			)
		}
	}

	return node, true
}

// checkBinding checks that a binding does not conflict with any preexisting
// bindings.  It logs appropriate errors if such a conflict exists.
func (w *Walker) checkBinding(binding *typing.Binding, bindTypePos *logging.TextPosition) bool {
	logBindingConflictError := func(mname string, binding *typing.Binding) {
		w.logError(
			fmt.Sprintf("Multiple implementations given for method `%s` bound to `%s`",
				mname,
				binding.MatchType.Repr(),
			),
			logging.LMKImport,
			bindTypePos,
		)
	}

	if mname, isConflict := w.SrcFile.LocalBindings.CheckBindingConflicts(binding); isConflict {
		logBindingConflictError(mname, binding)
		return false
	}

	if mname, isConflict := w.SrcPackage.GlobalBindings.CheckBindingConflicts(binding); isConflict {
		logBindingConflictError(mname, binding)
		return false
	}

	return true
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
			case "func_def", "annotated_method":
				var fnnode *common.HIRFuncDef
				var ok bool

				if methodBranch.Name == "func_def" {
					fnnode, ok = w.walkFuncDef(methodBranch, true)
				} else {
					methodNode, _, mok := w.walkAnnotatedDef(methodBranch)
					fnnode = methodNode.(*common.HIRFuncDef)
					ok = mok
				}

				if ok {
					name = fnnode.Name
					dt = fnnode.Type
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
						w.logError(
							"Type interface may not contain abstract methods",
							logging.LMKInterf,
							namePosition,
						)

						return nil, false
					} else {
						methodKind = typing.MKStandard
					}
				} else {
					return nil, false
				}
			case "special_def":
				if specNode, ok := w.walkMethodSpecial(it, methodBranch); ok {
					methodNodes = append(methodNodes, specNode)
					continue
				} else {
					return nil, false
				}
			}

			// handle generic methods
			dt, node = w.applyGenericContextToMethod(dt, node)

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

// walkFuncSpecial walks a function specialization
func (w *Walker) walkFuncSpecial(branch *syntax.ASTBranch) (common.HIRNode, bool) {
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
				// just use the global table to prevent specializations from
				// being applied across packages (big no-no)
				sym, ok := w.SrcPackage.GlobalTable[v.Value]

				if ok {
					if sym.DefKind != common.DefKindFuncDef {
						w.logError(
							"Function specialization may only be applied to generic functions",
							logging.LMKGeneric,
							v.Position(),
						)

						return nil, false
					}
				} else {
					if w.resolving {
						if _, ok := w.sharedOpaqueSymbolTable.LookupOpaque(w.SrcPackage.PackageID, v.Value); ok {
							// shared opaque symbol can only share things that aren't functions => specialization is invalid
							w.logError(
								"Function specialization may only be applied to generic functions",
								logging.LMKGeneric,
								v.Position(),
							)
						}
					} else {
						w.logError(
							fmt.Sprintf("Unable to find specialization local to current package named `%s`", v.Value),
							logging.LMKName,
							v.Position(),
						)
					}

					return nil, false
				}

				// must exist since we have confirmed this is a function node
				if gnode, ok := sym.DefNode.(*common.HIRGeneric); ok {
					// just use the builtin create generic instance method to
					// check the specialization parameters. We can actually just
					// leave the instance as pregenerated since it will be
					// skipped during generic evaluation (since a specialization
					// exists) and since we know there is a valid
					// specialization, we know all instances matching the
					// specialization are valid as well
					if _, ok = w.solver.CreateGenericInstance(gnode.Generic, genericSpecial.MatchingTypes, typeListBranch); !ok {
						return nil, false
					}

					for _, spec := range gnode.Specializations {
						if spec.Match(genericSpecial) {
							// duplicate/conflicting specialization
							w.logError(
								"Unable to define multiple specializations with for same type parameters",
								logging.LMKGeneric,
								typeListBranch.Position(),
							)
							return nil, false
						}
					}

					gnode.Specializations = append(gnode.Specializations, genericSpecial)
				} else {
					w.logError(
						"Function specialization is only valid on generic functions",
						logging.LMKGeneric,
						v.Position(),
					)
				}
			}
		}
	}

	// handle any parametric specialization before returning
	return w.applyGenericContextToSpecial(gt, genericSpecial, body), true
}

// walkMethodSpecial walks a method specialization
func (w *Walker) walkMethodSpecial(it *typing.InterfType, branch *syntax.ASTBranch) (common.HIRNode, bool) {
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
				if method, ok := it.Methods[v.Value]; ok {
					if method.Kind == typing.MKAbstract {
						w.logError(
							"Unable to define specialization for abstract method",
							logging.LMKGeneric,
							v.Position(),
						)
						return nil, false
					}

					if gt, ok = method.Signature.(*typing.GenericType); ok {
						// just use the builtin create generic instance method
						// to check the specialization parameters. We can
						// actually just leave the instance as pregenerated
						// since it will be skipped during generic evaluation
						// (since a specialization exists) and since we know
						// there is a valid specialization, we know all
						// instances matching the specialization are valid as
						// well
						if _, ok = w.solver.CreateGenericInstance(gt, genericSpecial.MatchingTypes, typeListBranch); !ok {
							return nil, false
						}

						for _, spec := range method.Specializations {
							if spec.Match(genericSpecial) {
								// duplicate/conflicting specialization
								w.logError(
									"Unable to define multiple specializations with for same type parameters",
									logging.LMKGeneric,
									typeListBranch.Position(),
								)
								return nil, false
							}
						}

						method.Specializations = append(method.Specializations, genericSpecial)
					} else {
						w.logError(
							"Function specialization is only valid on generic functions",
							logging.LMKGeneric,
							v.Position(),
						)
					}
				} else {
					w.LogUndefined(v.Value, v.Position())
					return nil, false
				}
			}
		}
	}

	// handle any parametric specialization before returning
	return w.applyGenericContextToSpecial(gt, genericSpecial, body), true
}

// walkAnnotatedDef walks an `annotated_def` or `annotated_method` node
func (w *Walker) walkAnnotatedDef(branch *syntax.ASTBranch) (common.HIRNode, typing.DataType, bool) {
	// store the outer annotation to restore it after this function is called
	outerAnnots := w.annotations
	defer func() {
		w.annotations = outerAnnots
	}()

	isMethod := branch.Name == "annotated_method"

	w.annotations = nil
	defNode := branch.BranchAt(1)
	for _, item := range branch.BranchAt(0).Content {
		// only branch is `annot_single`
		if annot, ok := item.(*syntax.ASTBranch); ok {
			var annotName string
			var annotArgs []string
			for _, elem := range annot.Content {
				if elemleaf, ok := elem.(*syntax.ASTLeaf); ok {
					if elemleaf.Kind == syntax.IDENTIFIER {
						annotName = elemleaf.Value
					} else if elemleaf.Kind == syntax.STRINGLIT {
						annotArgs = append(annotArgs, elemleaf.Value[1:len(elemleaf.Value)-1])
					}
				}
			}

			// duplicate annotations are not allowed
			if _, ok := w.annotations[annotName]; ok {
				w.logError(
					fmt.Sprintf("Annotation `%s` defined multiple times", annotName),
					logging.LMKAnnot,
					annot.Position(),
				)

				return nil, nil, false
			}

			if w.validateAnnotation(annotName, annotArgs, defNode.Name, annot.Position(), isMethod) {
				w.annotations[annotName] = annotArgs
			} else {
				return nil, nil, false
			}
		}
	}

	if isMethod {
		fdef, ok := w.walkFuncDef(defNode, isMethod)

		if ok {
			return fdef, fdef.Type, ok
		}

		return nil, nil, false
	}

	return w.walkDefRaw(defNode)
}

// validateAnnotation takes in the name of an annotation, any arguments it
// takes, and the name of the node it is applied to and determines whether or
// not that combination is valid.  It logs appropriate errors if not.
func (w *Walker) validateAnnotation(annotName string, annotArgs []string, defNodeName string, pos *logging.TextPosition, isMethod bool) bool {
	logAnnotArgError := func(expectedCount int) {
		w.logError(
			fmt.Sprintf("Annotation `%s` expects `%d` arguments; received `%d`", annotName, expectedCount, len(annotArgs)),
			logging.LMKAnnot,
			pos,
		)
	}

	// functions and operators are functionally the same for annotations as are
	// interface definitions and interface bindings
	switch defNodeName {
	case "func_def", "operator_def":
		switch annotName {
		case "external", "intrinsic", "introspect", "vec_unroll", "no_warn",
			"inline", "tail_rec", "hot_call", "no_inline":
			if len(annotArgs) != 0 {
				logAnnotArgError(0)
				return false
			} else if isMethod {
				if annotName == "external" || annotName == "intrinsic" {
					w.logError(
						fmt.Sprintf("Unable to apply annotation `%s` to method", annotName),
						logging.LMKAnnot,
						pos,
					)

					return false
				}
			}

			return true
		case "dll_import":
			if len(annotArgs) != 2 {
				logAnnotArgError(2)
				return false
			} else if isMethod {
				w.logError(
					"Unable to apply annotation `dll_import` to method",
					logging.LMKAnnot,
					pos,
				)

				return false
			}

			return true
		}
	case "interf_def", "interf_bind":
		if annotName == "introspect" {
			if len(annotArgs) != 0 {
				logAnnotArgError(0)
				return false
			}

			return true
		}
	case "type_def":
		switch annotName {
		case "packed":
			if len(annotArgs) != 0 {
				logAnnotArgError(0)
				return false
			}

			return true
		case "impl":
			if len(annotArgs) != 1 {
				logAnnotArgError(1)
				return false
			}

			return true
		}
	}

	// if we reach here, not other annotation matched
	w.logError(
		fmt.Sprintf("Annotation `%s` is not valid for %s", annotName, defNodeName),
		logging.LMKAnnot,
		pos,
	)
	return false
}

// walkOperatorDef walks an operator definitions
func (w *Walker) walkOperatorDef(branch *syntax.ASTBranch) (common.HIRNode, bool) {
	// operators aren't marked boxable since they can never be boxed
	opfn := &typing.FuncType{}
	od := &common.HIROperDef{
		Signature:   opfn,
		Annotations: w.annotations,
	}

	var opValue string
	var argsPos *logging.TextPosition
	for _, item := range branch.Content {
		if itembranch, ok := item.(*syntax.ASTBranch); ok {
			switch itembranch.Name {
			case "operator_value":
				// the formula in the argument determines which token is
				// actually going to act as our opkind.  For nodes of length 1
				// and 2 (all operators except slice), it will point to the
				// first token (0/2 and 1/2).  For the slice operator, it will
				// point to the second token (':', 2/2).
				od.OperKind = itembranch.LeafAt((itembranch.Len() - 1) / 2).Kind
				for _, item := range itembranch.Content {
					opValue += item.(*syntax.ASTLeaf).Value
				}
			case "generic_tag":
				if !w.primeGenericContext(itembranch, false) {
					return nil, false
				}
			case "signature":
				if args, inits, rttype, ok := w.walkSignature(itembranch, true); ok {
					argsPos = itembranch.BranchAt(0).Position()

					opfn.Args = args
					od.Initializers = inits
					opfn.ReturnType = rttype
				} else {
					return nil, false
				}
			case "decl_func_body":
				od.Body = w.extractFuncBody(itembranch)
			}
		}
	}

	argsCount := len(opfn.Args)
	logExpectedArgCountError := func(expected int) {
		w.logError(
			fmt.Sprintf("Operator `%s` takes exactly %d arguments not %d", opValue, expected, argsCount),
			logging.LMKDef,
			argsPos,
		)
	}

	// validate that the number of arguments this operator definition accepts
	// makes sense for the arity of the operator (eg. `+` must take two
	// arguments)
	switch od.OperKind {
	case syntax.NOT, syntax.COMPL:
		// strictly unary operators
		if argsCount != 1 {
			logExpectedArgCountError(1)
			return nil, false
		}
	case syntax.MINUS:
		// the `-` operator (can either be unary or binary)
		if argsCount != 1 && argsCount != 2 {
			w.logError(
				fmt.Sprintf("Operator `-` can accept either 1 or 2 arguments not %d", argsCount),
				logging.LMKDef,
				argsPos,
			)

			return nil, false
		}
	default:
		// all other operators are binary
		if argsCount != 2 {
			logExpectedArgCountError(2)
			return nil, false
		}
	}

	// operators do not use standard generic wrapping (as they aren't defined as
	// symbols) and so we need to use a special generic context applicator for
	// them.  We need to ensure that defineOperator is given the full generic
	// signature -- thus why we call this first
	node, sig := w.applyGenericContextToOpDef(od)

	return node, w.defineOperator(od.OperKind, sig, opValue, argsPos)
}

// defineOperator takes an operator signature and attempts to define it within
// the current package.  It logs all necessary errors and returns a flag
// indicating whether or not the operator was defined successfully.  It also
// contextually checks for conflicting local overloads in the current file
func (w *Walker) defineOperator(opkind int, sig typing.DataType, opValue string, argPos *logging.TextPosition) bool {
	if sig, isConflict := w.SrcPackage.CheckOperatorConflicts(w.SrcFile, opkind, sig); isConflict {
		w.logError(
			fmt.Sprintf("Operator definition for `%s` conflicts with preexisting definition with signature `%s`", opValue, sig.Repr()),
			logging.LMKDef,
			argPos,
		)

		return false
	}

	// add the operator globally
	w.SrcPackage.OperatorDefinitions[opkind] = append(w.SrcPackage.OperatorDefinitions[opkind], &common.WhirlOperatorDefinition{
		Signature: sig,
		Exported:  w.declStatus == common.DSExported,
	})

	return true
}
