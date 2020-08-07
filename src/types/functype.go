package types

import (
	"fmt"
	"strings"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// FuncParam represents a function parameter used in the function data type
// (note that parameter value specifiers are not included)
type FuncParam struct {
	Name               string
	Type               DataType // actually contains value :)
	Optional, Variadic bool

	// Stored here to make copy ellision easier
	Constant bool
}

// NewFuncParam creates a new function parameter value
func NewFuncParam(n string, t DataType, o, v bool) *FuncParam {
	return &FuncParam{Name: n, Type: t, Optional: o, Variadic: v}
}

// Equals determines whether or not two function parameters are equal
func (fp *FuncParam) Equals(other *FuncParam) bool {
	return fp.compareNames(other.Name) && Equals(fp.Type, other.Type) && fp.Optional == other.Optional && fp.Variadic == other.Variadic
}

// typeRepr returns a string represents how the parameter appears in a type
// label (used in repr for function type)
func (fp *FuncParam) typeRepr() string {
	var prefix string

	if fp.Optional {
		prefix += "~"
	} else if fp.Variadic {
		prefix += "..."
	}

	return prefix + fp.Type.Repr()
}

// compareNames takes in the name of another parameter and determines if the
// function names are effectively equivalent (ie. unnamed parameters will match
// any named parameter)
func (fp *FuncParam) compareNames(otherName string) bool {
	if fp.Name == "" || otherName == "" {
		return true
	}

	return fp.Name == otherName
}

// FuncType represents the general function data type (both boxed and pure)
type FuncType struct {
	Params      []*FuncParam
	ReturnType  DataType
	Async       bool
	Boxed       bool
	Boxable     bool
	ConstStatus int
}

// NewFuncType creates a new function type (boxable)
func NewFuncType(fparams []*FuncParam, rttype DataType, async bool, boxed bool, cs int) DataType {
	return &FuncType{Params: fparams, ReturnType: rttype, Async: async, Boxed: boxed, Boxable: true, ConstStatus: cs}
}

// NewIntrinsic creates a new instrinsic function data type
func NewIntrinsic(fparams []*FuncParam, rttype DataType) DataType {
	return &FuncType{Params: fparams, ReturnType: rttype, Boxable: false}
}

// function types don't coerce in any way (signature too well defined)
func (ft *FuncType) coerce(other DataType) bool {
	return false
}

// function types can be cast between normal functions and coroutines
func (ft *FuncType) cast(other DataType) bool {
	if oft, ok := other.(*FuncType); ok {
		// boxed status also doesn't matter here (up box generated if necessary)
		return ft.compareParams(oft) && Equals(ft.ReturnType, oft.ReturnType) && ft.Boxable == oft.Boxable
	}

	return false
}

func (ft *FuncType) equals(other DataType) bool {
	if oft, ok := other.(*FuncType); ok {
		// note: an intrinsic signature and a non-intrinsic signature for the
		// same function are not identical
		return ft.compareParams(oft) && Equals(ft.ReturnType, oft.ReturnType) && ft.Async == oft.Async && ft.Boxed == oft.Boxed && ft.Boxable == oft.Boxable
	}

	return false
}

// compareParams compares to function's parameters for equality (or usable
// equality)
func (ft *FuncType) compareParams(oft *FuncType) bool {
	if len(ft.Params) != len(oft.Params) {
		return false
	}

	for i, p := range ft.Params {
		if !p.Equals(oft.Params[i]) {
			return false
		}
	}

	return true
}

// SizeOf a function type depends on whether or not it is boxed: if it is not,
// it is simply the size of a pointer.  If it is not boxed, then it is the size
// of a 2 pointers (one for func pointer, one for state pointer)
func (ft *FuncType) SizeOf() uint {
	if ft.Boxed {
		return util.PointerSize * 2
	}

	return util.PointerSize
}

// AlignOf a function is always the size of a pointer since it will either
// itself be a pointer or be a struct whose largest element is a pointer, either
// of which work here
func (ft *FuncType) AlignOf() uint {
	return util.PointerSize
}

// Repr generates a type label for the function type and returns it as the repr
// string
func (ft *FuncType) Repr() string {
	paramReprs := make([]string, len(ft.Params))

	for _, p := range ft.Params {
		paramReprs = append(paramReprs, p.typeRepr())
	}

	paramRepr := strings.Join(paramReprs, ",")

	if ft.Async {
		return fmt.Sprintf("async(%s)(%s)", paramRepr, ft.ReturnType.Repr())
	}

	return fmt.Sprintf("func(%s)(%s)", paramRepr, ft.ReturnType.Repr())
}

func (ft *FuncType) copyTemplate() DataType {
	newParams := make([]*FuncParam, len(ft.Params))

	for i, p := range ft.Params {
		newParams[i] = &FuncParam{Type: p.Type.copyTemplate(), Name: p.Name, Optional: p.Optional, Variadic: p.Variadic}
	}

	return &FuncType{
		ReturnType:  ft.ReturnType.copyTemplate(),
		Params:      newParams,
		Async:       ft.Async,
		Boxed:       ft.Boxed,
		Boxable:     ft.Boxable,
		ConstStatus: ft.ConstStatus,
	}
}
