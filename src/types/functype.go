package types

type FuncParam struct {
	Type               DataType
	Optional, Variadic bool
}

type FuncType struct {
	Params     map[string]*FuncParam
	ReturnType DataType
	Async      bool
}

func (ft *FuncType) coerce(other DataType) bool {
	return ft == other
}

func (ft *FuncType) cast(other DataType) bool {
	return false
}

type FuncGroup struct {
	Group []*FuncType
	Name  string
}
