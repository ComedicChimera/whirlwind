package types

type DataType interface {
	cast(other DataType) bool
	coerce(other DataType) bool
	equals(other DataType) bool
}

func Generalize(dt ...DataType) (DataType, bool) {
	return nil, false
}

func InstOf(elem DataType, set DataType) bool {
	return false
}

func CoerceTo(src DataType, dest DataType) bool {
	return false
}

func CastTo(src DataType, dest DataType) bool {
	return false
}
