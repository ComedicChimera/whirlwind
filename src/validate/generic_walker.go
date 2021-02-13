package validate

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/logging"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/typing"
)

// primeGenericContext should be called whenever a `generic_tag` node is
// encountered in a definition.  It parses the tag and appropriately populates
// the `TypeParams` slice with all of the encounter type parameters.  It returns
// `true` if the parsing was successful.  This function does populate the
// `Unknowns` field and sets `FatalDefError` appropriately.
func (w *Walker) primeGenericContext(genericTag *syntax.ASTBranch) bool {
	wc := make([]*typing.WildcardType, genericTag.Len()-2)
	names := make(map[string]struct{})

	for i, item := range genericTag.Content[1 : genericTag.Len()-1] {
		param := item.(*syntax.ASTBranch)

		name := param.LeafAt(0).Value
		if _, ok := names[name]; ok {
			names[name] = struct{}{}
		} else {
			w.logFatalDefError(
				fmt.Sprintf("Multiple type parameters declared with name `%s`", name),
				logging.LMKName,
				param.Content[0].Position(),
			)

			return false
		}

		if param.Len() == 1 {
			wc[i%2] = &typing.WildcardType{Name: name}
		} else if typeList, ok := w.walkOffsetTypeList(param, 2, 0); ok {
			wc[i%2] = &typing.WildcardType{
				Name:        name,
				Restrictors: typeList,
			}
		} else {
			return false
		}
	}

	w.genericCtx = wc
	return true
}

// applyGenericContext should be called at the end of every definition.  This
// function checks if a generic context exists (in `TypeParams`).  If so,
// returns the appropriately constructed `HIRGeneric` and clears the generic
// context for the next definition.  If there is no context, it simply returns
// the definition passed in.
func (w *Walker) applyGenericContext(node common.HIRNode, dt typing.DataType) (common.HIRNode, typing.DataType) {
	if w.genericCtx == nil {
		return node, dt
	}

	// if there is a selfType, then we know that selfType stores a pre-built
	// GenericType type used for selfType referencing (so we don't need to
	// create a new one at all)
	var gt *typing.GenericType
	if w.selfType != nil {
		gt = w.selfType.(*typing.GenericType)
	} else {
		gt = &typing.GenericType{
			TypeParams: w.genericCtx,
			Template:   dt,
		}
	}

	// wrap our generic into a HIRGeneric
	gen := &common.HIRGeneric{
		Generic:     gt,
		GenericNode: node,
	}

	// find the symbol of the declared data type so its type can be overwritten
	// with the generic type (should always succeed b/c this is called
	// immediately after a definition)
	symbol, _ := w.Lookup(w.currentDefName)
	symbol.Type = gt

	// update algebraic variants of open generic algebraic types
	if at, ok := dt.(*typing.AlgebraicType); ok {
		if !at.Closed {
			for variName := range at.Variants {
				// we know the variant exists -- we can just look it up
				vs, _ := w.Lookup(variName)

				vs.Type = &typing.GenericAlgebraicVariantType{
					GenericParent: gt,
					VariantName:   variName,
				}
			}
		}
	}

	w.genericCtx = nil
	return gen, symbol.Type
}

// createGenericInstance creates a new instance of the given generic type or
// generic opaque type.  It will log an error if the type passed in is not
// generic.
func (w *Walker) createGenericInstance(generic typing.DataType, genericPos *logging.TextPosition, paramsBranch *syntax.ASTBranch) (typing.DataType, bool) {
	params, ok := w.walkTypeList(paramsBranch)

	if !ok {
		return nil, false
	}

	switch v := generic.(type) {
	case *typing.GenericType:
		if gi, ok := w.solver.CreateGenericInstance(v, params, paramsBranch); ok {
			return gi, ok
		} else {
			w.fatalDefError = true
			return nil, ok
		}
	case *typing.OpaqueGenericType:
		if v.EvalType == nil {
			ogi := &typing.OpaqueGenericInstanceType{
				OpaqueGeneric:    v,
				TypeParams:       params,
				TypeParamsBranch: paramsBranch,
			}

			v.Instances = append(v.Instances, ogi)

			return ogi, true
		}

		if gi, ok := w.solver.CreateGenericInstance(v.EvalType, params, paramsBranch); ok {
			return gi, ok
		} else {
			w.fatalDefError = true
			return nil, ok
		}

	}

	// not a generic type -- error
	w.logFatalDefError(
		fmt.Sprintf("Unable to pass type parameters to non-generic type `%s`", generic.Repr()),
		logging.LMKTyping,
		genericPos,
	)

	return nil, false
}

// setSelfType sets the selfType field accounting for generic context
func (w *Walker) setSelfType(st typing.DataType) {
	// if there is no generic context, then we can just assign as is
	if w.genericCtx == nil {
		w.selfType = st
	}

	// otherwise, we need to wrap it in a generic to be used later
	w.selfType = &typing.GenericType{
		TypeParams: w.genericCtx,
		Template:   st,
	}
}
