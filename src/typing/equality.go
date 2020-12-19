package typing

import "reflect"

func (pt *PrimitiveType) Equals(other DataType) bool {
	return reflect.DeepEqual(pt, other)
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

// func (ft *FuncType) Equals(other DataType) bool {
// 	if oft, ok := other.(*FuncType); ok {

// 	}

// 	return false
// }

// func (st *StructType) Equals(other DataType) bool {
// 	if ost, ok := other.(*StructType); ok {

// 	}

// 	return false
// }
