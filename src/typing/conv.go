package typing

// This file describes the conversions between different types (coercions and
// casts) and contains the implementations for CoerceTo and CastTo for all of
// the primary data types.

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
