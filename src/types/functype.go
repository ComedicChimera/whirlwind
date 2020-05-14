package types

// FuncParam represents a function parameter used in the function
// data type (note that parameter value specifiers are not included)
type FuncParam struct {
	Type               DataType // actually contains value :)
	Optional, Variadic bool
}

// FuncType represents the general function data type (both boxed and pure)
type FuncType struct {
	Params     map[string]*FuncParam
	ReturnType DataType
	Async      bool
	Boxed      bool
	Boxable    bool
}

func (ft *FuncType) coerce(other DataType) bool {
	return false
}

func (ft *FuncType) cast(other DataType) bool {
	return false
}

// SizeOf a function type depends on whether or not
// it is boxed: if it is not, it is simply the size of
// a pointer.  If it is not boxed, then it is the size
// of a 2 pointers (one for func pointer, one for state pointer)
func (ft *FuncType) SizeOf(other DataType) uint {
	if ft.Boxed {
		return PointerSize * 2
	}

	return PointerSize
}

// AlignOf a function is always the size of a pointer
// since it will either itself be a pointer or be a struct
// whose largest element is a pointer, either of which work here
func (ft *FuncType) AlignOf(other DataType) uint {
	return PointerSize
}
