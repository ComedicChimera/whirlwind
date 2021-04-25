package typing

// Equals is the true equality function for comparing two types.  It's specific
// benefit over the builtin `equals` methods on every type is that it extracts
// the inner types by default before performing its comparison.  This is the
// comparison method that should be used externally.
func Equals(a, b DataType) bool {
	return InnerType(a).equals(InnerType(b))
}

// pureEquals checks for "pure equality".  This function is pretty much
// exclusively used in method binding and primarily handles aliases.
func pureEquals(a, b DataType) bool {
	var innerA, innerB DataType

	if aat, ok := a.(*AliasType); ok {
		innerA = aat
	} else {
		innerA = InnerType(a)
	}

	if bat, ok := b.(*AliasType); ok {
		innerB = bat
	} else {
		innerB = InnerType(b)
	}

	return innerA.equals(innerB)
}

// InnerType extracts the inner type of any data type (that is the type it is
// enclosing).  For most types, this function is simply an identity.  However,
// in the case of something like an OpaqueType, it extracts the evaluated type.
func InnerType(dt DataType) DataType {
	switch v := dt.(type) {
	case *GenericInstanceType:
		return v.MemoizedGenerate
	case *OpaqueType:
		// if the opaque type hasn't been evaluated yet, we have nothing to
		// extract (and the equality test always fails)
		if v.EvalType == nil {
			return v
		}

		return v.EvalType
	case *OpaqueGenericType:
		// same logic as for OpaqueType
		if v.EvalType == nil {
			return v
		}

		return v.EvalType
	case *OpaqueGenericInstanceType:
		// same logic as for OpaqueType
		if v.MemoizedGenerate == nil {
			return v
		}

		return v.MemoizedGenerate
	case *AliasType:
		return v.TrueType
	case *UnknownType:
		if v.EvalType == nil {
			return v
		}

		return v.EvalType
	default:
		return dt
	}
}

// OperatorsConflict tests if two operator signatures conflict
func OperatorsConflict(a, b DataType) bool {
	extractFuncType := func(f DataType) *FuncType {
		switch v := f.(type) {
		case *FuncType:
			return v
		case *GenericType:
			return v.Template.(*FuncType)
		}

		// unreachable
		return nil
	}

	afn := extractFuncType(a)
	bfn := extractFuncType(b)

	// operators don't allow indefinite or optional arguments => if the lengths
	// aren't equal, then the operators don't conflict
	if len(afn.Args) != len(bfn.Args) {
		return false
	}

	// positional matching is all the matters not name (since operators are
	// never invoked with named arguments)
	for i, arg := range afn.Args {
		// only one need not match
		if !Equals(arg.Val.Type, bfn.Args[i].Val.Type) {
			return false
		}
	}

	return true
}
