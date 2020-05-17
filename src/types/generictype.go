package types

type GenericType struct {
	Template   DataType
	Forms      [][]DataType
	TypeParams []*TypeParam
}

func (gt *GenericType) CreateGenerate(typeList []DataType) (DataType, bool) {
	for i := range typeList {
		if !gt.TypeParams[i].InitWithType(typeList[i]) {
			return nil, false
		}
	}

	gt.Forms = append(gt.Forms, typeList)
	return gt.copyTemplate(), true
}

func (gt *GenericType) copyTemplate() DataType {
	return nil
}

type TypeParam struct {
	placeholderRef *DataType
	restrictors    []DataType
}

func (tp *TypeParam) InitWithType(dt DataType) bool {
	if len(tp.restrictors) == 0 {
		*tp.placeholderRef = dt
		return true
	}

	for _, r := range tp.restrictors {
		if CoerceTo(r, dt) {
			*tp.placeholderRef = dt
			return true
		}
	}

	return false
}
