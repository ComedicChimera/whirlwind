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
