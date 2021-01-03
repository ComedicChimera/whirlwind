package validate

import (
	"fmt"

	"github.com/ComedicChimera/whirlwind/src/common"
	"github.com/ComedicChimera/whirlwind/src/logging"
	"github.com/ComedicChimera/whirlwind/src/syntax"
	"github.com/ComedicChimera/whirlwind/src/typing"
)

var validTypeSetIntrinsics = map[string]struct{}{
	"Vector":         struct{}{},
	"TypedVector":    struct{}{},
	"IntegralVector": struct{}{},
	"Tuple":          struct{}{},
}

// WalkDef walks the AST of any given definition and attempts to determine if it
// can be defined.  If it can, it returns the produced HIR node and the value
// `true`. If it cannot, then it determines first if the only errors are due to
// missing symbols, in which case it returns a map of unknowns and `true`.
// Otherwise, it returns `false`. All unmentioned values for each case will be
// nil. This is the main definition analysis function and accepts a `definition`
// node.  This function is mainly intended to work with the Resolver and
// PackageAssembler.
func (w *Walker) WalkDef(dast *syntax.ASTBranch) (common.HIRNode, map[string]*UnknownSymbol, bool) {
	def, dt, ok := w.walkDefRaw(dast)

	if ok {
		// apply generic context before returning definition; we can assume that
		// if we reached this point, there are no unknowns to account for
		return w.applyGenericContext(def, dt), nil, true
	}

	// clear the generic context
	w.genericCtx = nil

	// handle fatal definition errors
	if w.fatalDefError {
		w.fatalDefError = false

		return nil, nil, false
	}

	// collect and clear our unknowns after they have collected for the definition
	unknowns := w.unknowns
	w.clearUnknowns()

	return nil, unknowns, true
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
				if !w.primeGenericContext(v) {
					return nil, false
				}
			case "newtype":
				if w.hasFlag("intrinsic") {
					w.logInvalidIntrinsic(name, "defined type", namePosition)
					return nil, false
				}

				// TODO: new type implementation
			case "typeset":
				if types, ok := w.walkOffsetTypeList(v, 1); ok {
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
					w.fatalDefError = true
					return nil, false
				}
			}
		case *syntax.ASTLeaf:
			switch v.Kind {
			case syntax.CLOSED:
				closedType = true
			case syntax.IDENTIFIER:
				name = v.Value
				namePosition = v.Position()
			}
		}
	}

	if _, ok := dt.(*typing.AlgebraicType); !ok && closedType {
		logging.LogError(
			w.Context,
			fmt.Sprintf("`closed` property not applicable on type `%s`", dt.Repr()),
			logging.LMKUsage,
			dast.Content[0].Position(),
		)

		w.fatalDefError = true
		return nil, false
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

	return &common.HIRTypeDef{
		Sym:        symbol,
		FieldInits: fieldInits,
	}, true
}
