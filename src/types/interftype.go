package types

const (
	MK_VIRTUAL = iota
	MK_IMPLEMENTED
	MK_ABSTRACT
)

type Method struct {
	FnType *FuncType
	Kind   int
}

type TypeInterf struct {
	Methods map[string]*Method
}
