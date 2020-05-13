package types

type FuncParam struct {
	Type               DataType // actually contains value :)
	Optional, Variadic bool
}

type FuncType struct {
	Params     map[string]*FuncParam
	ReturnType DataType
	Async      bool
}

func (ft *FuncType) coerce(other DataType) bool {
	return false
}

func (ft *FuncType) cast(other DataType) bool {
	return false
}
