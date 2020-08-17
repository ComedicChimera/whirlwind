package types

import (
	"strings"

	"github.com/ComedicChimera/whirlwind/src/util"
)

// GenericType is an abstraction used to represent a generic data type (type
// that accepts type parameters).  It also associates the various monomorphic
// type forms of the generic type
type GenericType struct {
	Template   DataType
	Forms      []*GenericForm
	TypeParams []*TypeParam

	// This field doesn't technically "belong" on the generic type but rather on
	// the GenericNode.  However, since we cannot easily access the generic node
	// after it is created, it is preferrable to store the variant references
	// here.  (Not a perfect solution but good enough).  The value of each
	// position is the index in the enclosing node that `HIRVariant` is located
	// at.  This is the simplest way to avoid an import cycle (since Golang has
	// one up the a** about that...)
	Variants []int
}

// NewGenericType creates a new generic type based on the given template
// accepting the given type parameters (with restrictors)
func NewGenericType(template DataType, tps []*TypeParam) *GenericType {
	return &GenericType{Template: template, TypeParams: tps}
}

// CreateGenerate tries to get a generate based on the given type list.
// If it is successful, it returns the generate.  If not, it returns nil.
// true = successful, false = failed.  It will pull a preexisting generate
// for the type list if at all possible (either from the type table or from
// the forms table so it doesn't have to recreate the generate each time)
func (gt *GenericType) CreateGenerate(typeList []DataType) (DataType, bool) {
	for _, forms := range gt.Forms {
		if generate, isMatch := forms.Match(typeList); isMatch {
			return generate, true
		}
	}

	for i := range typeList {
		if !gt.TypeParams[i].InitWithType(typeList[i]) {
			return nil, false
		}
	}

	generate := gt.Template.copyTemplate()
	gt.Forms = append(gt.Forms, &GenericForm{Generate: generate, TypeList: typeList})

	// clear the stored values from the type parameters (so that
	// comparisons work properly after this generate is created)
	for _, p := range gt.TypeParams {
		*p.PlaceholderRef = nil
	}

	return generate, true
}

// All type relational operators between raw generics are nonsensical (no way
// of determining which form, too polymorphic) and are therefore undefined

func (gt *GenericType) coerce(other DataType) bool {
	util.LogMod.LogFatal("Unable to apply coercion to a generic type")
	return false
}

func (gt *GenericType) cast(other DataType) bool {
	util.LogMod.LogFatal("Unable to apply casting to a generic type")
	return false
}

func (gt *GenericType) equals(other DataType) bool {
	util.LogMod.LogFatal("Unable to test equality between a generic type and another type")
	return false
}

// SizeOf a generic type is undefined (causes fatal error)
func (gt *GenericType) SizeOf() uint {
	util.LogMod.LogFatal("Unable to calculate the size of a generic type")
	return 0
}

// AlignOf a generic type is undefined (causes fatal error)
func (gt *GenericType) AlignOf() uint {
	util.LogMod.LogFatal("Unable to calculate the alignment of a generic type")
	return 0
}

// Repr of a generic type attempts to rebuild the type label
// that created it (ie. TemplateRepr<...TypeParamNames>)
func (gt *GenericType) Repr() string {
	typeParamSlice := make([]string, len(gt.TypeParams))

	for i := range typeParamSlice {
		typeParamSlice[i] = gt.TypeParams[i].Name
	}

	return gt.Template.Repr() + "<" + strings.Join(typeParamSlice, ", ") + ">"
}

// copyTemplate on a generic is undefined
func (gt *GenericType) copyTemplate() DataType {
	util.LogMod.LogFatal("Unable to perform a type copy on a generic")
	return nil
}

// TypeParam represents a generic type parameter. They contain
// references that are shared by all of the TypeParamPlaceholders
// associated with this type parameter so that the type value
// can be filled in whenever this parameter is initialized
type TypeParam struct {
	Name           string
	Restrictors    []DataType
	PlaceholderRef *DataType
}

// InitWithType attempts to fill in the placeholder types with
// the value of this type parameter.  It fails if the type
// does not match any restrictors that the type parameter has.
// If the type parameter has no restrictors, then it all type
// values are considered value and this function always succeeds.
func (tp *TypeParam) InitWithType(dt DataType) bool {
	if len(tp.Restrictors) == 0 {
		*tp.PlaceholderRef = dt
		return true
	}

	for _, r := range tp.Restrictors {
		if CoerceTo(r, dt) {
			*tp.PlaceholderRef = dt
			return true
		}
	}

	return false
}

// GenericForm represents a given monomorphic generate of a
// generic type (it stores the type list required to create
// the generate as well as the generate itself: allows for
// reusability on the front-end and makes backend generation
// easier since it essentially implements generic monomorphism).
type GenericForm struct {
	TypeList []DataType
	Generate DataType
}

// Match compares a type list to the given generic form to see if
// the type list would produce an identical generate to the one
// represented by this form.  If so, it returns the already-
// generated generate.  Otherwise, it returns nil.
// true = successful match, false = no match (unsuccessful)
func (gf *GenericForm) Match(typeList []DataType) (DataType, bool) {
	if TypeListEquals(gf.TypeList, typeList) {
		return gf.Generate, true
	}

	return nil, false
}

// TypeParamPlaceholder is a data type that holds a reference to
// the value of a TypeParam that is updated everytime the type
// parameter is initialized.  It is effectively a stand-in for a
// type parameter in any given location (eg. Type<T>, T is a TPP)
type TypeParamPlaceholder struct {
	TPName         string
	PlaceholderRef *DataType
}

// all type relational functions relate based on the underlying type
// if it exists, otherwise they assume that the comparison succeeds
func (tpp *TypeParamPlaceholder) coerce(other DataType) bool {
	if *tpp.PlaceholderRef == nil {
		return true
	}

	return CoerceTo(*tpp.PlaceholderRef, other)
}

func (tpp *TypeParamPlaceholder) cast(other DataType) bool {
	if *tpp.PlaceholderRef == nil {
		return true
	}

	return CastTo(*tpp.PlaceholderRef, other)
}

func (tpp *TypeParamPlaceholder) equals(other DataType) bool {
	if *tpp.PlaceholderRef == nil {
		return true
	}

	return Equals(*tpp.PlaceholderRef, other)
}

// Repr of a placeholder is just the name of the type
// parameter it is standing in for (rebuilding label)
func (tpp *TypeParamPlaceholder) Repr() string {
	return tpp.TPName
}

// SizeOf a placeholder is undefined it has not been given
// a type value. Otherwise, it is the size of the type value.
func (tpp *TypeParamPlaceholder) SizeOf() uint {
	if *tpp.PlaceholderRef == nil {
		util.LogMod.LogFatal("Unable to calculate size of unsatisfied placeholder")
	}

	return (*tpp.PlaceholderRef).SizeOf()
}

// AlignOf a placeholder is undefined it has not been given a
// type value. Otherwise, it is the alignment of the type value.
func (tpp *TypeParamPlaceholder) AlignOf() uint {
	if *tpp.PlaceholderRef == nil {
		util.LogMod.LogFatal("Unable to calculate alignment of unsatisfied placeholder")
	}

	return (*tpp.PlaceholderRef).AlignOf()
}

// copyTemplate on a type parameter placeholder removes the reference
//boxing on it and returns the current value of its type reference
func (tpp *TypeParamPlaceholder) copyTemplate() DataType {
	return (*tpp.PlaceholderRef).copyTemplate()
}

// TODO: fix GenericInstance (...)
// // GenericInstance represents a usage of a generic where the actual generic form
// // can't yet by created (most likely due to the fact that the generic to which
// // type parameters are being passed is an OpenType/TypeParam).  It stores
// // information about what the root OpenType is and what type parameters are
// // passed in.  NOTE: the GenericInstances should be stored in a table like
// // OpenTypes so that they can be checked later, ideally with some kind of
// // position (again in the same way that Open Types).
// type GenericInstance struct {
// 	OpenGeneric *OpenType
// 	TypeParams  []DataType
// }

// // GenericInstances can
