package types

// Unify finds the unified type of a set if possible
// of data types (unified meaning type all types
// in the set are able to coerced to: set != typeset here)
func Unify(dts ...DataType) (DataType, bool) {
	unifiedType := dts[0]

	for i := 1; i < len(dts); i++ {
		dt := dts[i]

		if CoerceTo(dt, unifiedType) {
			continue
		} else if CoerceTo(unifiedType, dt) {
			unifiedType = dt
		} else {
			return Generalize(dts...)
		}
	}

	return unifiedType, true
}

// Generalize finds the lowest type set that can
// accurately represent the types it is given.
// If no such type set exists, it returns nil, false
// Note: Mainly meant for use in Unification
func Generalize(dt ...DataType) (DataType, bool) {
	return nil, false
}

// GetMethod checks if the data has the specified method and
// if it does returns the data type of that method and if
// not returns that it could not find a match for the given method
func GetMethod(dt DataType, methodName string) (DataType, bool) {
	return nil, false
}

// MaxSize is a utlity function that takes a set of data types
// and returns the SizeOf of the data type with largest size
func MaxSize(dts []DataType) uint {
	var maxSize uint

	for _, dt := range dts {
		dtSize := dt.SizeOf()

		if dtSize > maxSize {
			maxSize = dtSize
		}
	}

	return maxSize
}

// MaxAlign does the same thing as MaxSize but for alignment
func MaxAlign(dts []DataType) uint {
	var maxAlign uint

	for _, dt := range dts {
		dtAlign := dt.AlignOf()

		if dtAlign > maxAlign {
			maxAlign = dtAlign
		}
	}

	return maxAlign
}

// TypeListEquals compares two lists of data types for equality
func TypeListEquals(tla []DataType, tlb []DataType) bool {
	if len(tla) != len(tlb) {
		return false
	}

	for i, item := range tla {
		if !Equals(item, tlb[i]) {
			return false
		}
	}

	return true
}
