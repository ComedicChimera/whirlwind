package typing

func (pt *PrimitiveType) Equals(other DataType) bool {
	if opt, ok := other.(*PrimitiveType); ok {
		return pt.PrimKind == opt.PrimKind && pt.PrimSpec == opt.PrimSpec
	}

	return false
}

func (tt TupleType) Equals(other DataType) bool {
	// Imagine if Go had a map function... (writing before Go generics)
	if ott, ok := other.(TupleType); ok {
		for i, item := range tt {
			if !item.Equals(ott[i]) {
				return false
			}
		}

		return true
	}

	return false
}

func (vt *VectorType) Equals(other DataType) bool {
	if ovt, ok := other.(*VectorType); ok {
		return vt.ElemType.Equals(ovt.ElemType) && vt.Size == ovt.Size
	}

	return false
}

func (rt *RefType) Equals(other DataType) bool {
	if ort, ok := other.(*RefType); ok {
		return (rt.ElemType.Equals(ort.ElemType) &&
			rt.Constant == ort.Constant &&
			rt.Owned == ort.Owned &&
			rt.Block == ort.Block &&
			rt.Global == ort.Global)
	}

	return false
}

func (rt RegionType) Equals(other DataType) bool {
	// `other is RegionType`... wouldn't that be nice?
	if _, ok := other.(RegionType); ok {
		// all region types are equivalent
		return true
	}

	return false
}

func (ft *FuncType) Equals(other DataType) bool {
	if oft, ok := other.(*FuncType); ok {
		if len(ft.Params) != len(oft.Params) {
			return false
		}

		// Function parameters must be in the same order
		for i, param := range ft.Params {
			oparam := oft.Params[i]

			// Two parameters must either have the same name or have no name and
			// be in the correct position (as is the case for arguments in the
			// function data type)
			if param.Name == oparam.Name || param.Name == "" || oparam.Name == "" {
				// We don't care about volatility and *value* constancy when
				// comparing function signatures since both values don't
				// actually effect what can be passed in and what will be
				// produced by the function.  Also, value constancy being
				// ignored doesn't cause any actual constancy violations since
				// whatever is passed in will be copied and reference constancy
				// holds.
				if !(param.Val.Type.Equals(oparam.Val.Type) &&
					param.Indefinite == oparam.Indefinite &&
					param.Optional == oparam.Optional) {
					return false
				}
			} else {
				return false
			}

		}

		// intrinsic functions are not the same (nor should they be treated) as
		// regular functions (although I doubt this will ever come up since
		// instrinsics can't be be boxed anyway ¯\_(ツ)_/¯).  Constancy can't be
		// emulated/denoted in a function type literal and so that field doesn't
		// matter for the purposes of type equality (it is however taken into
		// account when comparing interface methods).  Whether or not a function
		// is boxed should also be irrelevant here.
		return ft.Async == oft.Async && ft.Boxable == oft.Boxable
	}

	return false
}

// Equality for all defined types is trivial since two defined types must refer
// to the same declaration if their name and package ID are the same since only
// one such type by any particular name may be declared in the same package.
// Thus, we can just compare the name and package ID to test for equality.  It
// does make one wish could had generics though.

func (st *StructType) Equals(other DataType) bool {
	if ost, ok := other.(*StructType); ok {
		return st.Name == ost.Name && st.SrcPackageID == ost.SrcPackageID
	}

	return false
}

func (it *InterfType) Equals(other DataType) bool {
	if oit, ok := other.(*InterfType); ok {
		return it.Name == oit.Name && it.SrcPackageID == oit.SrcPackageID
	}

	return false
}

func (at *AlgebraicType) Equals(other DataType) bool {
	if oat, ok := other.(*AlgebraicType); ok {
		return at.Name == oat.Name && at.SrcPackageID == oat.SrcPackageID
	}

	return false
}

func (ts *TypeSet) Equals(other DataType) bool {
	if ots, ok := other.(*TypeSet); ok {
		return ts.Name == ots.Name && ts.SrcPackageID == ots.SrcPackageID
	}

	return false
}

// Algebraic instances are equal if they have the same parent and name and
// equivalent values in the same positions (since values are initialized
// positionally).
func (ai *AlgebraicInstance) Equals(other DataType) bool {
	if oai, ok := other.(*AlgebraicInstance); ok {
		if !ai.Parent.Equals(oai.Parent) {
			return false
		}

		if len(ai.Values) != len(oai.Values) {
			return false
		}

		for i, value := range ai.Values {
			if !value.Equals(oai.Values[i]) {
				return false
			}
		}

		return ai.Name == oai.Name
	}

	return false
}
