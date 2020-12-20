package typing

// This file describes the conversions between different types (coercions and
// casts) and contains the implementations for CoerceTo and CastTo for all of
// the primary data types.

// TypeConverter is a construct used for converting between the different
// types in Whirlwind.  It stores all the state necessary to facilitate
// conversions.  It facilitates two kinds of conversions: casts (explicit type
// casts using the `as` syntax) and coercions (implicit type casts).
// type TypeConverter struct {
// 	// Bindings stores a list of visible interface bindings for the given
// 	// file that corresponds to this converter.  This data structure is used
// 	// to modularize bindings between different packages and files.

// }

// Rule of Coercion:
// The rule of coercion specifies that a coercion should be legal between two
// things of (relatively) equivalent value.  That means no coercion should
// result in a significant change in the meaning of the value of a type or in
// that value itself.  Thus, `int` to `float` is a valid coercion since (for the
// most part), the numeric value of the `int` can be preserved across that
// coercion.  However, something like `int` to `uint` could change the numeric
// value of the original data significantly, and something like `bool` to `int`
// dramatically changes the meaning of the data stored in the boolean value.
// Thus, those two operations should be casts (requiring explicit denotation)
// and not coercions.

func (pt *PrimitiveType) CoerceFrom(other DataType) bool {
	// check for any (since all types can coerce to it)
	if pt.PrimKind == PrimKindUnit && pt.PrimSpec == 1 {
		return true
	}

	if opt, ok := other.(*PrimitiveType); ok {
		// integral, floating to floating
		if pt.PrimKind == PrimKindFloating {
			return opt.PrimKind == PrimKindIntegral || (opt.PrimKind == PrimKindFloating && opt.PrimSpec < pt.PrimSpec)
		} else if pt.PrimKind == PrimKindIntegral {
			// integral to integral
			if opt.PrimKind == PrimKindIntegral {
				// in order for the coercion to succeed, the two integrals must
				// both have the same signedness (that is both unsigned or both
				// signed) which since the PrimSpecs for integrals alternate
				// between signed and unsigned means they must be equal (mod 2)
				// and since coercion only applied upward (that is short to int
				// but not int to short), the current (this) PrimSpec must be
				// greater than the other PrimSpec
				return pt.PrimSpec%2 == opt.PrimSpec%2 && pt.PrimSpec > opt.PrimSpec
			}
		} else if pt.PrimKind == PrimKindText {
			// rune to string
			return pt.PrimSpec == 1 && opt.PrimSpec == 0
		}
	}

	return false
}

func (pt *PrimitiveType) CastTo(other DataType) bool {
	// any to some other type always succeeds (naively here)
	if pt.PrimKind == PrimKindUnit && pt.PrimSpec == 1 {
		return true
	}

	// primitives (outside of any) can only be cast to other primitives
	if opt, ok := other.(*PrimitiveType); ok {
		// all numeric types can be cast between each other
		if pt.Numeric() && opt.Numeric() {
			return true
		}

		// bool to integral
		return pt.PrimKind == PrimKindBoolean && opt.PrimKind == PrimKindIntegral
	}

	return false
}

func (tt TupleType) CoerceFrom(other DataType) bool {
	if ott, ok := other.(TupleType); ok {
		for i, item := range tt {
			if !item.CoerceFrom(ott[i]) {
				return false
			}
		}

		return true
	}

	return false
}

func (tt TupleType) CastTo(other DataType) bool {
	if ott, ok := other.(TupleType); ok {
		for i, item := range tt {
			if !item.CastTo(ott[i]) {
				return false
			}
		}

		return true
	}

	return false
}

func (vt *VectorType) CoerceFrom(other DataType) bool {
	if ovt, ok := other.(*VectorType); ok {
		return vt.ElemType.CoerceFrom(ovt.ElemType) && vt.Size == ovt.Size
	}

	return false
}

func (vt *VectorType) CastTo(other DataType) bool {
	if ovt, ok := other.(*VectorType); ok {
		return vt.ElemType.CastTo(ovt.ElemType) && vt.Size == ovt.Size
	}

	return false
}

// The coercion possible on references is non-const to const.  This has to do
// with the fact that &int and &uint although similar at the surface mean very
// different things: there is no inherent relation between the memory the point
// to.  A reference's identity is based on what it points to and its value.
// Thus, such a coercion would not only mean a duplication of the reference
// itself but also the memory it points to -- since such duplication could mean
// a wide variety of different things it is better to simply not allow such
// coercions. Constancy coercion only applies to match regular constancy rules
// (where a variable can "coerce" to a constant).
func (rt *RefType) CoerceFrom(other DataType) bool {
	if ort, ok := other.(*RefType); ok {
		return (rt.Owned == ort.Owned &&
			rt.Block == ort.Block &&
			rt.Global == ort.Global &&
			rt.ElemType.Equals(ort.ElemType) &&
			rt.Constant && !ort.Constant)
	}

	return false
}

// No additional casts are possible on references.  Converting a reference to
// an integer value is an unsafe operation (that must be performed using an
// intrinsic stored in `unsafe`).  All other casts are invalid because either
// they violate the memory model or fundamentally reinterpret the memory the
// reference points to which is also considered unsafe.
func (rt *RefType) CastTo(other DataType) bool {
	return false
}

// Since all regions are inherently equal, not other coercions or casts are
// necessary (since equality must be checked separately from coercion).
func (rt RegionType) CoerceFrom(other DataType) bool {
	return false
}

func (rt RegionType) CastTo(other DataType) bool {
	return false
}

// Functions cannot be coerced or cast (because doing so would require creating
// an implicit wrapper around the original function that casts the arguments on
// every call -- you can't "cast" functions in LLVM, the operation makes no
// sense)
func (ft *FuncType) CoerceFrom(other DataType) bool {
	return false
}

func (ft *FuncType) CastTo(other DataType) bool {
	return false
}

// Structs cannot be coerced mainly because is structure is unique both in its
// name and package ID -- where it is declared and what is represents are
// fundamental aspects of its meaning.  Moreover, if two structs have
// differently named fields then coercion and casting makes no sense since there
// is no correspondence between the fields.
func (st *StructType) CoerceFrom(other DataType) bool {
	return false
}

// They can, however, be cast.  However, they can only be cast if they have
// identical fields since there is some relation between the data the structs
// store (this also allows for structs with the same name and fields in
// different packages to be cast between in each other).  Consider the example
// of the `Vec2` and `Point2D` structs.  They have the same fields (`x` and `y`)
// and types `int`. Although they are different structs representing different
// things, a cast still makes sense since it is rational to reinterpret the data
// of `Point2D` to actually represent the components of a `Vec2`.  By contrast,
// even if two structs if the same field names, if the fields are different in
// any way (even two coercible types), then the cast will fail since the two
// fields may have very different meanings.
func (st *StructType) CastTo(other DataType) bool {
	if ost, ok := other.(*StructType); ok {
		// Packing changes the representation of the data so it should be
		// semantic illogical to cast between a packed and unpacked struct.
		if st.Packed != ost.Packed {
			return false
		}

		if len(st.Fields) != len(ost.Fields) {
			return false
		}

		for name, field := range st.Fields {
			if ofield, ok := ost.Fields[name]; !ok || !field.Equals(ofield) {
				return false
			}
		}

		// In order to have truly identical fields, their inherits must also match
		if len(st.Inherits) != len(ost.Inherits) {
			return false
		}

		// the inherits don't need to be in the same order so we will need to do
		// linear search on every inherit.  Luckily, most structs will only have
		// one or two inherits so that search is fairly trivial.  Moreover, it
		// is VERY space inefficient to try to store the inherits as map since
		// it would have to ordered both by name and package ID.
		for _, inherit := range st.Inherits {
			for _, oinherit := range ost.Inherits {
				if !inherit.Equals(oinherit) {
					return false
				}
			}
		}

		return true
	}

	return false
}

// Interfaces can coerce and be cast to any type that fully implements it
func (it *InterfType) CoerceFrom(other DataType) bool {
	// TODO: implementation of interface bindings required
	return false
}

func (it *InterfType) CastTo(other DataType) bool {
	// TODO: implementation of interface bindings required
	return false
}

// Coercion and casting for algebraic types works the same way it does for
// struct types (and is designed following the same logic)
func (at *AlgebraicType) CoerceFrom(other DataType) bool {
	return false
}

func (at *AlgebraicType) CastTo(other DataType) bool {
	if oat, ok := other.(*AlgebraicType); ok {
		if len(at.Instances) != len(oat.Instances) {
			return false
		}

		for _, instance := range at.Instances {
			if oinstance, ok := oat.Instances[instance.Name]; !ok || !instance.Equals(oinstance) {
				return false
			}
		}

		return true
	}

	return false
}

// Algebraic instances and type sets cannot be cast at all
func (ai *AlgebraicInstance) CoerceFrom(other DataType) bool {
	return false
}

func (ai *AlgebraicInstance) CastTo(other DataType) bool {
	return false
}

func (ts *TypeSet) CoerceFrom(other DataType) bool {
	return false
}

func (ts *TypeSet) CastTo(other DataType) bool {
	return false
}
