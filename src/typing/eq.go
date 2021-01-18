package typing

// Equals is the true equality function for comparing two types.  It's specific
// benefit over the builtin `equals` methods on every type is that it extracts
// the inner types by default before performing its comparison.  This is the
// comparison method that should be used externally.
func Equals(a, b DataType) bool {
	return InnerType(a).equals(InnerType(b))
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
	default:
		return dt
	}
}
