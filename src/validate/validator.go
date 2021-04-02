package validate

import (
	"whirlwind/common"
	"whirlwind/syntax"
	"whirlwind/typing"
)

// PredicateValidator is a data structure that implements Stage 3 of compilation
// -- predicate validation.  It validates all expressions and function bodies --
// also handling generic evaluation.  The relationship between the Walker and
// the the Validator is essentially that the validator feeds branches to the
// Walker which does the actual checking.  It works to coordinate the walker
type PredicateValidator struct {
	walkers []*Walker
}

func NewPredicateValidator(walkers map[*common.WhirlFile]*Walker) *PredicateValidator {
	pv := &PredicateValidator{}

	for _, w := range walkers {
		pv.walkers = append(pv.walkers, w)
	}

	return pv
}

// Validate runs the predicate validation algorithm on the given package. All
// validation functions don't return boolean flags since at this stage we want
// to try and catch as many local errors as we can (efficiently) and
// logging.ShouldProceed() should serve a suitable indicator of this stage's
// success
func (pv *PredicateValidator) Validate() {
	// generics stores a slice of all the generic nodes encountered so
	// that they can be processed once standard walking has occured
	var generics []*common.HIRGeneric

	for _, w := range pv.walkers {
		for _, node := range w.SrcFile.Root.Elements {
			if gen, ok := node.(*common.HIRGeneric); ok {
				generics = append(generics, gen)
			} else {
				pv.validateNode(w, node)
			}
		}
	}
}

// validateNode is used to validate is a single HIR node declared in the
// HIRRoot. Notably, this function does not handle `*HIRGeneric` -- that should
// be handled externally
func (pv *PredicateValidator) validateNode(w *Walker, node common.HIRNode) {
	switch v := node.(type) {
	case *common.HIRFuncDef:
		// make sure the function body is not empty before walking it
		if v.Body != nil {
			if body, ok := w.walkFuncBody(v.Body.(*common.HIRIncomplete), v.Type); ok {
				v.Body = body
			}
		}

		// handle argument initializers
		for name, init := range v.Initializers {
			for _, arg := range v.Type.Args {
				if arg.Name == name {
					if expr, ok := w.walkInitializer(init.(*common.HIRIncomplete), arg.Val.Type); ok {
						v.Initializers[name] = expr
					}
				}
			}
		}

	case *common.HIROperDef:
		// walk func body and initializers
	case *common.HIRInterfDef:
		// walk interf body
	case *common.HIRInterfBind:
		// walk interf body
	case *common.HIRTypeDef:
		// walk initializers
	case *common.HIRSpecialDef:
		// walk special body
	}
}

// walkFuncBody walks a branch (wrapped in a HIRIncomplete) that was stored as a
// function body.  It also accepts the data type (signature) of the function
// whose body is walks -- this is used as the function context
func (w *Walker) walkFuncBody(inc *common.HIRIncomplete, fn *typing.FuncType) (common.HIRNode, bool) {
	// create our contextual function scope
	w.pushFuncScope(fn)

	// make sure the scope is popped before we exit (cleanup)
	defer w.popScope()

	branch := (*syntax.ASTBranch)(inc)
	if branch.Name == "expr" {
		if expr, ok := w.walkExpr(branch); ok {
			if w.coerceTo(expr, fn.ReturnType) {
				return expr.(common.HIRNode), true
			} else {
				w.logCoercionError(expr.Type(), fn.ReturnType, branch.Position())
			}
		}
	} else {
		// TODO: block walking
	}

	return nil, false
}

// walkInitializer is used to walk an initializer branch (wrapped in a
// HIRIncomplete) and check it against the expected type it was given.
func (w *Walker) walkInitializer(inc *common.HIRIncomplete, expected typing.DataType) (common.HIRNode, bool) {
	if expr, ok := w.walkExpr((*syntax.ASTBranch)(inc)); ok {
		if w.coerceTo(expr, expected) {
			return expr.(common.HIRNode), true
		} else {
			w.logCoercionError(expr.Type(), expected, (*syntax.ASTBranch)(inc).Position())
		}
	}

	return nil, false
}
